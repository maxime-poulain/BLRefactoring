using System.Text;
using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Common;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The strategic-design documents, held to the model they describe.
/// </summary>
/// <remarks>
/// <c>docs/strategic-design/</c> says where the boundaries are, which aggregate sits inside which,
/// and which business facts the boards account for. All three claims are about the code, and all
/// three go stale the same way: somebody adds an aggregate, or an event, and the documents go on
/// describing the model that existed before.
/// <para>
/// The same treatment the README's project graph already gets. A document nothing checks is a
/// document that is correct on the day it is merged, and this repository does not accept that
/// anywhere else. See ADR 0023.
/// </para>
/// </remarks>
public sealed class StrategicDesignRules
{
    /// <summary>A bounded-context section, written <c>## Context — Name</c>.</summary>
    private static readonly Regex ContextHeading =
        new(@"^##\s+Context\s+—\s+(?<name>.+?)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// An aggregate declared under a <c>### Aggregates</c> heading: a bullet opening on its name.
    /// </summary>
    /// <remarks>
    /// The whole line is not searched, and that is the difference between a rule and a formality.
    /// The prose under that heading goes on naming the aggregates — "`Training` names its owner by
    /// `TrainerId`" — so a scan of the block would go on finding `Training` after its declaration
    /// had been deleted. Measured, not assumed: the first version of this rule passed against a
    /// document with the bullet removed.
    /// </remarks>
    private static readonly Regex Declaration =
        new(@"^\s*-\s+`(?<name>[^`]+)`", RegexOptions.Compiled);

    private static IReadOnlyList<Type> DomainTypes { get; } = [.. Solution.Domain.DeclaredTypes()];

    private static IEnumerable<Type> Aggregates =>
        DomainTypes.Where(type => typeof(IAggregateRoot).IsAssignableFrom(type) && !type.IsAbstract);

    private static IEnumerable<Type> DomainEvents =>
        DomainTypes.Where(type => typeof(IDomainEvent).IsAssignableFrom(type) && !type.IsAbstract);

    /// <summary>
    /// Every aggregate, is placed in exactly one bounded context.
    /// </summary>
    /// <remarks>
    /// The rule that matters most of the three. An aggregate is the unit a boundary is drawn around,
    /// so an aggregate nobody placed is the precise moment the document stops being true — and
    /// placing one in two contexts is the other way of saying the boundary was never decided.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0023",
        "every aggregate belongs to exactly one bounded context, and the document says which")]
    public void EveryAggregate_IsPlacedInExactlyOneBoundedContext()
    {
        var contexts = BoundedContexts();

        Aggregates
            .Selected("aggregate")
            .Select(aggregate => (
                aggregate,
                placed: contexts
                    .Where(context => AggregatesDeclaredIn(context.Value).Contains(aggregate.Name))
                    .Select(context => context.Key)
                    .ToArray()))
            .Where(pair => pair.placed.Length != 1)
            .Select(pair => pair.placed.Length == 0
                ? $"{pair.aggregate.Name} appears under no '### Aggregates' heading in " +
                  "docs/strategic-design/bounded-contexts.md. Place it in the context that owns it"
                : $"{pair.aggregate.Name} is placed in {pair.placed.Length} contexts at once " +
                  $"({string.Join(", ", pair.placed)}). An aggregate belongs to one boundary")
            .ShouldHold();
    }

    /// <summary>
    /// Every domain event, appears in the event storming.
    /// </summary>
    /// <remarks>
    /// The boards claim to account for the business facts this system raises. A seventh event that
    /// no board mentions makes that claim false without making anything red — which is the failure
    /// this rule exists to convert into a build failure.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0023",
        "the boards account for every business fact the domain raises, not for the ones already drawn")]
    public void EveryDomainEvent_AppearsInTheEventStorming()
    {
        var boards = Document("event-storming.md");

        DomainEvents
            .Selected("domain event")
            .Where(domainEvent => !boards.Contains(domainEvent.Name, StringComparison.Ordinal))
            .Select(domainEvent =>
                $"{domainEvent.Name} is raised by the domain and named nowhere in " +
                "docs/strategic-design/event-storming.md. Add it to a board, or to the design-level table")
            .ShouldHold();
    }

    /// <summary>
    /// Every context on the map, has its own section.
    /// </summary>
    /// <remarks>
    /// Two documents describing one set of boundaries drift the moment one is edited alone. Checked
    /// in both directions: a context drawn on the map and described nowhere is a box with no
    /// content, and a context described at length and left off the map is invisible to whoever only
    /// looks at the picture.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0023",
        "the map and the descriptions name the same contexts, in both directions")]
    public void EveryContextOnTheMap_HasItsOwnSection()
    {
        var described = BoundedContexts().Keys.ToHashSet(StringComparer.Ordinal);
        var drawn = ContextsOnTheMap();

        drawn
            .Selected("context on the map")
            .Where(context => !described.Contains(context))
            .Select(context =>
                $"context-map.md lists '{context}', and bounded-contexts.md has no " +
                $"'## Context — {context}' section for it")
            .Concat(described
                .Where(context => !drawn.Contains(context))
                .Select(context =>
                    $"bounded-contexts.md describes '{context}', and it is missing from the table of " +
                    "contexts in context-map.md"))
            .ShouldHold();
    }

    /// <summary>
    /// The bounded-context sections, keyed by name.
    /// </summary>
    /// <remarks>
    /// A section runs from its own heading to the next level-two heading, so prose that follows the
    /// last context — the "Not decided" section — belongs to no context and is read by nothing here.
    /// That is deliberate: the hypotheses kept at the end of that document are explicitly not
    /// contexts, and a rule that swept them up would make them look like decisions.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> BoundedContexts()
    {
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        var body = new StringBuilder();
        string? current = null;

        foreach (var line in Lines("bounded-contexts.md"))
        {
            var heading = ContextHeading.Match(line);

            if (heading.Success)
            {
                Close(sections, current, body);
                current = heading.Groups["name"].Value;
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Close(sections, current, body);
                current = null;
                continue;
            }

            if (current is not null)
            {
                body.AppendLine(line);
            }
        }

        Close(sections, current, body);

        if (sections.Count == 0)
        {
            // Vacuity, and the only way this rule could go green while checking nothing: a heading
            // style changed, every section came back empty, and "no aggregate is misplaced" became
            // true by finding no context at all.
            throw new InvalidOperationException(
                "docs/strategic-design/bounded-contexts.md declares no '## Context — Name' section. " +
                "Every rule here reads those headings; with none of them found, they assert nothing.");
        }

        return sections;
    }

    /// <summary>The aggregates declared under a section's <c>### Aggregates</c> heading.</summary>
    private static IReadOnlySet<string> AggregatesDeclaredIn(string section)
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);
        var inside = false;

        foreach (var line in section.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                inside = line.Trim().Equals("### Aggregates", StringComparison.Ordinal);
                continue;
            }

            var declaration = inside ? Declaration.Match(line) : Match.Empty;

            if (declaration.Success)
            {
                declared.Add(declaration.Groups["name"].Value);
            }
        }

        return declared;
    }

    /// <summary>The first column of the table under <c>## Contexts on this map</c>.</summary>
    private static IReadOnlySet<string> ContextsOnTheMap()
    {
        var listed = new HashSet<string>(StringComparer.Ordinal);
        var inside = false;

        foreach (var line in Lines("context-map.md"))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inside = line.Trim().Equals("## Contexts on this map", StringComparison.Ordinal);
                continue;
            }

            if (!inside || !line.StartsWith('|'))
            {
                continue;
            }

            var first = line.Split('|', StringSplitOptions.None)[1].Trim();

            // The header row and the separator under it are part of the table's syntax, not of it.
            if (first.Length == 0 || first == "Context" || first.All(character => character == '-'))
            {
                continue;
            }

            listed.Add(first);
        }

        return listed;
    }

    private static void Close(Dictionary<string, string> sections, string? current, StringBuilder body)
    {
        if (current is not null)
        {
            sections[current] = body.ToString();
        }

        body.Clear();
    }

    private static IEnumerable<string> Lines(string document) =>
        Document(document).Split('\n').Select(line => line.TrimEnd('\r'));

    private static string Document(string name) =>
        SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, "docs", "strategic-design", name));
}
