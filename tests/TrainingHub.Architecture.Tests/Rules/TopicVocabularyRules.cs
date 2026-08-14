using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Blazor.Client.Pages.Catalog;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The browser's copies of the closed set of subjects, held against the set itself (ADR 0069).
/// </summary>
/// <remarks>
/// A topic is a shelf: the domain owns the set, and the browser keeps three copies of it that no
/// build compares to anything. The picker offers the names. A dictionary translates a name into a
/// custom property. The sheet declares the property. None of the three references the domain — the
/// browser sees the generated client and nothing else — so each is a list somebody typed, and a
/// list somebody typed is a list somebody will forget.
/// <para>
/// Each failure is quiet in its own way, which is why they are worth a rule rather than a review.
/// A subject missing from the picker is a subject nobody can ever file a training under: the API
/// would accept it, the index would store it, the facet would show it, and no screen offers it. A
/// subject missing from the dictionary wears the neutral tone, which is the right answer for a name
/// from outside and the wrong one for a name the domain admits. A property the dictionary names and
/// the sheet does not declare paints nothing at all — a browser resolves an undefined custom
/// property to nothing rather than complaining, so the spine simply is not there.
/// </para>
/// <para>
/// Checked in both directions on every half. A name the browser offers that the domain refuses is
/// a chip whose training the API rejects, and a hue nothing can reach is a shelf that was renamed
/// and left its color behind.
/// </para>
/// </remarks>
public sealed partial class TopicVocabularyRules
{
    /// <summary>
    /// The list the create-training picker offers.
    /// </summary>
    private const string ThePicker =
        "src/Web/TrainingHub.Blazor/TrainingHub.Blazor.Client/Models/Topic.cs";

    /// <summary>
    /// The one translation from a topic's name to the shelf hue it wears.
    /// </summary>
    private const string TheDictionary =
        "src/Web/TrainingHub.Blazor/TrainingHub.Blazor.Client/Pages/Catalog/CatalogTopics.cs";

    /// <summary>
    /// The sheet that declares the hues.
    /// </summary>
    private const string TheSheet =
        "src/Web/TrainingHub.Blazor/TrainingHub.Blazor/wwwroot/app.css";

    /// <summary>
    /// The answer the dictionary gives a name the closed set does not admit.
    /// </summary>
    private const string NeutralHue = "var(--th-spine-neutral)";

    /// <summary>
    /// The same tone as the sheet declares it.
    /// </summary>
    private const string NeutralVariable = "--th-spine-neutral";

    /// <summary>
    /// Every topic the domain declares, is offered by the browser.
    /// </summary>
    /// <remarks>
    /// The picker's set compared to the domain's, both ways. Order is not compared: the picker
    /// declares the order it offers them in, which is a presentation decision and nobody else's.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0069",
        "the browser offers the closed set of subjects the domain admits, and no other")]
    public void EveryTopicTheDomainDeclares_IsOfferedByTheBrowser()
    {
        var admitted = Topic.GetTopics().Select(topic => topic.Name).ToHashSet(StringComparer.Ordinal);
        var offered = TrainingHub.Blazor.Client.Models.Topic.All.ToHashSet(StringComparer.Ordinal);

        admitted
            .Selected("topic the domain declares")
            .Where(name => !offered.Contains(name))
            .Select(name =>
                $"the domain admits '{name}' and '{ThePicker}' does not offer it. A subject absent " +
                "from the picker is one nobody can file a training under, however willingly the " +
                "API would accept it (ADR 0069)")
            .ShouldHold();

        offered
            .Selected("topic the browser offers")
            .Where(name => !admitted.Contains(name))
            .Select(name =>
                $"'{ThePicker}' offers '{name}' and the domain does not admit it. The chip is a " +
                "request the boundary refuses, reported to whoever picked it as their mistake " +
                "(ADR 0069)")
            .ShouldHold();
    }

    /// <summary>
    /// Every topic the domain declares, owns a shelf hue.
    /// </summary>
    /// <remarks>
    /// Two halves that can fail separately, and a third closing the sheet the other way. The
    /// neutral tone is exempt on both sides by name: it is the fallback for what the set does not
    /// admit, so it is declared without being claimed on purpose.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0069",
        "a topic is a shelf, and it wears one hue everywhere it appears")]
    public void EveryTopicTheDomainDeclares_OwnsAShelfHue()
    {
        var declared = SpineVariables(Text(TheSheet));

        var claimed = Topic.GetTopics()
            .ToDictionary(topic => topic.Name, topic => CatalogTopics.SpineVar(topic.Name));

        claimed
            .Selected("topic the domain declares")
            .Where(topic => string.Equals(topic.Value, NeutralHue, StringComparison.Ordinal))
            .Select(topic =>
                $"'{topic.Key}' falls through to the neutral tone. The dictionary in " +
                $"'{TheDictionary}' answers neutral for a name it does not know, which leaves a " +
                "topic the domain admits looking like one it does not (ADR 0069)")
            .ShouldHold();

        claimed
            .Where(topic => !string.Equals(topic.Value, NeutralHue, StringComparison.Ordinal))
            .Selected("topic claiming a hue of its own")
            .Where(topic => !declared.Contains(Variable(topic.Value)))
            .Select(topic =>
                $"'{topic.Key}' claims '{topic.Value}' and '{TheSheet}' declares no " +
                $"'{Variable(topic.Value)}'. A browser resolves an undefined custom property to " +
                "nothing at all, which reads as a defect nobody can reproduce from the markup " +
                "(ADR 0069)")
            .ShouldHold();

        var reachable = claimed.Values.Select(Variable).ToHashSet(StringComparer.Ordinal);

        declared
            .Where(variable => !string.Equals(variable, NeutralVariable, StringComparison.Ordinal))
            .Selected("shelf hue the sheet declares")
            .Where(variable => !reachable.Contains(variable))
            .Select(variable =>
                $"'{TheSheet}' declares '{variable}' and no topic claims it. A hue nothing can " +
                "reach is a shelf that was renamed or removed and left its color behind " +
                "(ADR 0069)")
            .ShouldHold();
    }

    /// <summary>
    /// A file of the tree, named the way this class names one.
    /// </summary>
    private static string Text(string relative) => SourceTree.ReadText(Path.Combine(
        SourceTree.RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>
    /// Every shelf hue the sheet declares, the neutral tone among them.
    /// </summary>
    private static HashSet<string> SpineVariables(string sheet) =>
        SpineDeclaration()
            .Matches(sheet)
            .Select(declaration => declaration.Groups["variable"].Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The custom property a <c>var(…)</c> reference names.
    /// </summary>
    private static string Variable(string reference) => reference["var(".Length..^1];

    [GeneratedRegex(@"^\s*(?<variable>--th-spine-[a-z-]+):", RegexOptions.Multiline)]
    private static partial Regex SpineDeclaration();
}
