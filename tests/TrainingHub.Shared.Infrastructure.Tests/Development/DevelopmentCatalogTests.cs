using AwesomeAssertions;
using TrainingHub.Shared.Infrastructure.Development;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Development;

/// <summary>
/// The written corpus, put through the value objects that will refuse it (ADR 0079).
/// </summary>
/// <remarks>
/// The seeder builds its data through the domain rather than around it, which means a blueprint
/// with a title of a hundred and one characters is not a bad row — it is a seeder that stops
/// halfway through, on a developer's machine, with half a catalog written. This suite is where that
/// is found instead: every string in the corpus goes through the factory that owns its rule, with
/// no database anywhere.
/// <para>
/// It is also the only place the corpus is checked for the property that makes it worth writing —
/// that between them the twelve areas file something under all sixteen subjects. A facet nobody
/// ever populates is indistinguishable from a facet that is broken.
/// </para>
/// </remarks>
public sealed class DevelopmentCatalogTests
{
    /// <summary>
    /// Every blueprint, is a training the domain accepts.
    /// </summary>
    /// <remarks>
    /// All four fields at once rather than one test each: what is being asserted is that the corpus
    /// is admissible, and a reader who sees this fail wants the offending title, not the name of
    /// the field it was in.
    /// </remarks>
    [Fact]
    public void EveryBlueprint_IsATrainingTheDomainAccepts()
    {
        var refusals = Blueprints()
            .SelectMany(blueprint => new[]
            {
                Refusal("title", blueprint.Title, TrainingTitle.Create(blueprint.Title)),
                Refusal("description", blueprint.Title, TrainingDescription.Create(blueprint.Description)),
                Refusal("prerequisites", blueprint.Title, TrainingPrerequisites.Create(blueprint.Prerequisites)),
                Refusal("acquired skills", blueprint.Title, AcquiredSkills.Create(blueprint.AcquiredSkills))
            })
            .Where(refusal => refusal is not null)
            .ToArray();

        refusals.Should().BeEmpty();
    }

    /// <summary>
    /// Every topic a blueprint names, is one the domain admits.
    /// </summary>
    /// <remarks>
    /// Ordinal and case-sensitive, exactly as <c>Topic.TryFromName</c> is: a corpus writing
    /// <c>"devops"</c> would be refused at creation time by the aggregate and by nothing before it.
    /// </remarks>
    [Fact]
    public void EveryTopicABlueprintNames_IsOneTheDomainAdmits()
    {
        var unknown = Blueprints()
            .SelectMany(blueprint => blueprint.Topics.Select(topic => (blueprint.Title, topic)))
            .Where(named => !Topic.TryFromName(named.topic, out _))
            .Select(named => $"'{named.Title}' is filed under '{named.topic}', which the domain does not admit")
            .ToArray();

        unknown.Should().BeEmpty();
    }

    /// <summary>
    /// Every blueprint, carries at least one topic.
    /// </summary>
    /// <remarks>
    /// A training with no topic is legal in the domain and invisible in the catalog's facets, which
    /// makes it the one shape a development corpus should never contain.
    /// </remarks>
    [Fact]
    public void EveryBlueprint_CarriesAtLeastOneTopic() =>
        Blueprints()
            .Where(blueprint => blueprint.Topics.Count == 0)
            .Should().BeEmpty();

    /// <summary>
    /// The corpus, covers every subject the domain admits.
    /// </summary>
    [Fact]
    public void TheCorpus_CoversEverySubjectTheDomainAdmits()
    {
        var filed = Blueprints()
            .SelectMany(blueprint => blueprint.Topics)
            .ToHashSet(StringComparer.Ordinal);

        Topic.GetTopics()
            .Select(topic => topic.Name)
            .Where(name => !filed.Contains(name))
            .Should().BeEmpty("a subject nothing is ever filed under leaves a facet that is empty " +
                              "rather than absent, and an empty facet reads as a defect");
    }

    /// <summary>
    /// Every biography the composition can produce, is one the domain accepts.
    /// </summary>
    /// <remarks>
    /// The whole cross product rather than a sample: an opening and a closing are joined by a
    /// space, so the only thing that can break is the total length, and the longest pair is the one
    /// nobody would have thought to try.
    /// </remarks>
    [Fact]
    public void EveryBiographyTheCompositionCanProduce_IsOneTheDomainAccepts()
    {
        var refusals = DevelopmentCatalog.Areas
            .SelectMany(area => area.Biographies)
            .SelectMany(opening => DevelopmentCatalog.TeachingNotes
                .Select(closing => $"{opening} {closing}"))
            .Select(bio => Refusal("bio", bio, Bio.Create(bio)))
            .Where(refusal => refusal is not null)
            .ToArray();

        refusals.Should().BeEmpty();
    }

    /// <summary>
    /// Every name the pools can pair, is one the domain accepts.
    /// </summary>
    [Fact]
    public void EveryNameThePoolsCanPair_IsOneTheDomainAccepts()
    {
        var refusals = DevelopmentCatalog.Firstnames
            .SelectMany(firstname => DevelopmentCatalog.Lastnames
                .Select(lastname => (firstname, lastname)))
            .Select(pair => Refusal(
                "name", $"{pair.firstname} {pair.lastname}", Name.Create(pair.firstname, pair.lastname)))
            .Where(refusal => refusal is not null)
            .ToArray();

        refusals.Should().BeEmpty();
    }

    /// <summary>
    /// Every area, offers more blueprints than one trainer can own.
    /// </summary>
    /// <remarks>
    /// The precondition the placement depends on and does not check: a trainer takes distinct
    /// blueprints from their own area, so an area holding fewer than the maximum catalog size would
    /// silently hand out fewer trainings than the dataset counted on.
    /// </remarks>
    [Fact]
    public void EveryArea_OffersMoreBlueprintsThanOneTrainerCanOwn() =>
        DevelopmentCatalog.Areas
            .Where(area => area.Blueprints.Count < DevelopmentDataset.MaximumTrainingsPerTrainer)
            .Should().BeEmpty();

    /// <summary>
    /// Every area, names its blueprints distinctly.
    /// </summary>
    /// <remarks>
    /// A title is unique per trainer and compared case-insensitively, and a trainer only ever draws
    /// from one area — so two blueprints of one area sharing a title is the one duplication that
    /// would reach the domain's refusal.
    /// </remarks>
    [Fact]
    public void EveryArea_NamesItsBlueprintsDistinctly() =>
        DevelopmentCatalog.Areas
            .Where(area => area.Blueprints
                .Select(blueprint => blueprint.Title)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != area.Blueprints.Count)
            .Select(area => area.Name)
            .Should().BeEmpty();

    private static IEnumerable<TrainingBlueprint> Blueprints() =>
        DevelopmentCatalog.Areas.SelectMany(area => area.Blueprints);

    /// <summary>
    /// What the domain said about one string, or <see langword="null"/> when it accepted it.
    /// </summary>
    private static string? Refusal<T>(string field, string subject, Result<T> result) =>
        result.Match<string?>(
            _ => null,
            errors => $"'{subject}' has an unacceptable {field}: " +
                      string.Join("; ", errors.Select(error => error.ErrorMessage)));
}
