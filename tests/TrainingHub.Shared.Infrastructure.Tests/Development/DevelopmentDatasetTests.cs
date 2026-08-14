using AwesomeAssertions;
using TrainingHub.Shared.Infrastructure.Development;

// The project under test has a namespace of its own called Email, so the value object is aliased
// exactly as the seeder aliases it — and for the same reason.
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Xunit;
using ContactEmail = TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects.Email;

namespace TrainingHub.Shared.Infrastructure.Tests.Development;

/// <summary>
/// The placement: exactly the requested number of trainings, nobody over the limit, nobody with
/// none, and the same answer twice (ADR 0079).
/// </summary>
/// <remarks>
/// Every property here is one the seeder relies on and cannot check for itself. It runs against a
/// database, one trainer at a time, inside a transaction each — by the time it discovers that the
/// dataset asked for six trainings from a trainer allowed five, it has already written a hundred
/// people.
/// </remarks>
public sealed class DevelopmentDatasetTests
{
    /// <summary>
    /// The instant the newest training is dated at. Fixed, because determinism is the subject.
    /// </summary>
    private static readonly DateTime Anchor = new(2026, 8, 14, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// The size the product's own seeding asks for.
    /// </summary>
    private const int Wanted = 500;

    /// <summary>
    /// Build, the requested size, holds exactly that many trainings.
    /// </summary>
    /// <remarks>
    /// Exact rather than approximate, and by construction rather than by trimming: nothing is added
    /// or removed after the sizes are drawn.
    /// </remarks>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(40)]
    [InlineData(Wanted)]
    public void Build_TheRequestedSize_HoldsExactlyThatManyTrainings(int wanted) =>
        DevelopmentDataset.Build(wanted, Anchor)
            .Sum(trainer => trainer.Trainings.Count)
            .Should().Be(wanted);

    /// <summary>
    /// Build, every trainer, owns between one and five trainings.
    /// </summary>
    /// <remarks>
    /// The upper bound is the dataset's own, and it is checked against the domain's rather than
    /// restated: a catalog of six would be refused by <c>Training.CreateAsync</c> only at ten, so
    /// this is the bound that keeps a seeded trainer able to add a training by hand afterwards.
    /// </remarks>
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(40)]
    [InlineData(Wanted)]
    public void Build_EveryTrainer_OwnsBetweenOneAndFiveTrainings(int wanted)
    {
        var sizes = DevelopmentDataset.Build(wanted, Anchor)
            .Select(trainer => trainer.Trainings.Count)
            .ToArray();

        sizes.Should().OnlyContain(size =>
            size >= DevelopmentDataset.MinimumTrainingsPerTrainer
            && size <= DevelopmentDataset.MaximumTrainingsPerTrainer);

        DevelopmentDataset.MaximumTrainingsPerTrainer.Should().BeLessThan(
            Training.MaximumPerTrainer,
            "a seeded catalog sitting on the domain's quota would refuse the first training " +
            "anybody created by hand");
    }

    /// <summary>
    /// Build, five hundred trainings, draws enough trainers to be worth paging.
    /// </summary>
    /// <remarks>
    /// A lower bound rather than a number: the count follows from the weights, and pinning it would
    /// turn a change of taste into a failing test. What matters is that the administration's list
    /// of trainers is longer than one page (ADR 0029).
    /// </remarks>
    [Fact]
    public void Build_FiveHundredTrainings_DrawsEnoughTrainersToBeWorthPaging() =>
        DevelopmentDataset.Build(Wanted, Anchor)
            .Should().HaveCountGreaterThan(100);

    /// <summary>
    /// Build, twice with the same arguments, answers the same dataset.
    /// </summary>
    /// <remarks>
    /// The property the whole design turns on. Without it a screenshot stops matching the database
    /// on the next start-up, an integration test asserting a title becomes a coin toss, and the
    /// seeder's idempotence — which keys on the username — stops meaning anything.
    /// </remarks>
    [Fact]
    public void Build_TwiceWithTheSameArguments_AnswersTheSameDataset() =>
        DevelopmentDataset.Build(Wanted, Anchor)
            .Should().BeEquivalentTo(
                DevelopmentDataset.Build(Wanted, Anchor),
                options => options.WithStrictOrdering());

    /// <summary>
    /// Build, every account name, is unique and spelled the way identity admits.
    /// </summary>
    /// <remarks>
    /// Case-insensitively unique, because Identity normalizes a username before comparing it — two
    /// people differing only in case would be one account, and the second creation would fail
    /// halfway through the run.
    /// </remarks>
    [Fact]
    public void Build_EveryAccountName_IsUniqueAndSpelledTheWayIdentityAdmits()
    {
        var trainers = DevelopmentDataset.Build(Wanted, Anchor);

        trainers
            .Select(trainer => trainer.Username.ToUpperInvariant())
            .Should().OnlyHaveUniqueItems();

        trainers
            .Where(trainer => !trainer.Username.All(
                character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '.'))
            .Should().BeEmpty("Identity's default character set admits no accent, and the seeder " +
                              "would be told so one account at a time");
    }

    /// <summary>
    /// Build, every address, is unique and one the domain accepts.
    /// </summary>
    /// <remarks>
    /// Identity is configured to require a unique address, so a duplicate is a refusal rather than
    /// a curiosity — and the address is also a contact email on the aggregate, which has its own
    /// factory to satisfy.
    /// </remarks>
    [Fact]
    public void Build_EveryAddress_IsUniqueAndOneTheDomainAccepts()
    {
        var trainers = DevelopmentDataset.Build(Wanted, Anchor);

        trainers
            .Select(trainer => trainer.Email.ToUpperInvariant())
            .Should().OnlyHaveUniqueItems();

        trainers
            .Where(trainer => ContactEmail.Create(trainer.Email).Match(_ => false, _ => true))
            .Should().BeEmpty();
    }

    /// <summary>
    /// Build, every profile, is one the domain accepts.
    /// </summary>
    /// <remarks>
    /// The corpus is already checked field by field; what this adds is the composition — the
    /// biography as it was actually assembled, and the name as it was actually paired.
    /// </remarks>
    [Fact]
    public void Build_EveryProfile_IsOneTheDomainAccepts()
    {
        var trainers = DevelopmentDataset.Build(Wanted, Anchor);

        trainers.Where(trainer => Refused(Bio.Create(trainer.Bio))).Should().BeEmpty();
        trainers.Where(trainer => Refused(Name.Create(trainer.Firstname, trainer.Lastname))).Should().BeEmpty();
        trainers
            .Where(trainer => trainer.SuspensionReason is not null
                              && Refused(SuspensionReason.Create(trainer.SuspensionReason)))
            .Should().BeEmpty();
    }

    /// <summary>
    /// Build, every training, is one the domain accepts.
    /// </summary>
    [Fact]
    public void Build_EveryTraining_IsOneTheDomainAccepts()
    {
        var trainings = Trainings();

        trainings.Where(training => Refused(TrainingTitle.Create(training.Title))).Should().BeEmpty();
        trainings
            .Where(training => training.WithholdingReason is not null
                               && Refused(WithholdingReason.Create(training.WithholdingReason)))
            .Should().BeEmpty();
        trainings
            .SelectMany(training => training.Topics)
            .Where(topic => !Topic.TryFromName(topic, out _))
            .Should().BeEmpty();
    }

    /// <summary>
    /// Build, every trainer, owns titles that differ from each other.
    /// </summary>
    /// <remarks>
    /// A title is unique per trainer and compared case-insensitively. Across trainers it repeats on
    /// purpose — several people do teach the same course — and that is what makes the search index
    /// worth querying.
    /// </remarks>
    [Fact]
    public void Build_EveryTrainer_OwnsTitlesThatDifferFromEachOther() =>
        DevelopmentDataset.Build(Wanted, Anchor)
            .Where(trainer => trainer.Trainings
                .Select(training => training.Title)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != trainer.Trainings.Count)
            .Should().BeEmpty();

    /// <summary>
    /// Build, the creation instants, are distinct and span the whole window.
    /// </summary>
    /// <remarks>
    /// Strictly increasing rather than merely distinct: the catalog's newest-first page is one of
    /// the things this dataset exists to make testable by hand, and equal instants would leave the
    /// order decided by the identifier tiebreaker alone (ADR 0071).
    /// </remarks>
    [Fact]
    public void Build_TheCreationInstants_AreDistinctAndSpanTheWholeWindow()
    {
        var instants = Trainings().Select(training => training.CreatedOnUtc).ToArray();

        instants.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        instants.Should().OnlyContain(instant => instant.Kind == DateTimeKind.Utc);

        instants[^1].Should().BeOnOrBefore(Anchor);
        instants[0].Should().BeBefore(Anchor - (DevelopmentDataset.Window * 0.9));
    }

    /// <summary>
    /// Build, the minority states, are present and mutually exclusive.
    /// </summary>
    /// <remarks>
    /// Both directions matter. A dataset where everything is published never shows a reader what a
    /// withheld training looks like in the administration; a training that is both withdrawn and
    /// withheld is a state the domain has no way to represent, and the seeder would either drop one
    /// of the two silently or fail on the second transition.
    /// </remarks>
    [Fact]
    public void Build_TheMinorityStates_ArePresentAndMutuallyExclusive()
    {
        var trainings = Trainings();

        trainings.Where(training => training.IsWithdrawnByOwner).Should().NotBeEmpty();
        trainings.Where(training => training.WithholdingReason is not null).Should().NotBeEmpty();
        trainings.Where(training => training.IsPublished).Should().HaveCountGreaterThan(trainings.Length / 2);

        trainings
            .Where(training => training.IsWithdrawnByOwner && training.WithholdingReason is not null)
            .Should().BeEmpty();
    }

    /// <summary>
    /// Build, the sanctioned trainers, are a minority rather than nobody.
    /// </summary>
    [Fact]
    public void Build_TheSanctionedTrainers_AreAMinorityRatherThanNobody()
    {
        var trainers = DevelopmentDataset.Build(Wanted, Anchor);

        var suspended = trainers.Count(trainer => trainer.SuspensionReason is not null);

        suspended.Should().BeGreaterThan(0);
        suspended.Should().BeLessThan(trainers.Count / 4);
    }

    /// <summary>
    /// Build, the portraits, are drawn for most people and for not quite everybody.
    /// </summary>
    /// <remarks>
    /// The trainers without one are what makes the initials fallback (ADR 0074) visible on a page
    /// of real results rather than only in a unit test.
    /// </remarks>
    [Fact]
    public void Build_ThePortraits_AreDrawnForMostPeopleAndForNotQuiteEverybody()
    {
        var trainers = DevelopmentDataset.Build(Wanted, Anchor);

        trainers.Where(trainer => trainer.PortraitHue is not null).Should().NotBeEmpty();
        trainers.Where(trainer => trainer.PortraitHue is null).Should().NotBeEmpty();
        trainers.Where(trainer => trainer.PortraitHue is < 0 or >= 360).Should().BeEmpty();
    }

    /// <summary>
    /// Build, a size below one, is refused.
    /// </summary>
    [Fact]
    public void Build_ASizeBelowOne_IsRefused()
    {
        var build = () => DevelopmentDataset.Build(0, Anchor);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static SeededTraining[] Trainings() =>
        [.. DevelopmentDataset.Build(Wanted, Anchor).SelectMany(trainer => trainer.Trainings)];

    private static bool Refused<T>(Result<T> result) => result.Match(_ => false, _ => true);
}
