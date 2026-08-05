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
public sealed partial class StrategicDesignRules
{
    /// <summary>A bounded-context section, written <c>## Context — Name</c>.</summary>
    [GeneratedRegex(@"^##\s+Context\s+—\s+(?<name>.+?)\s*$")]
    private static partial Regex ContextHeading { get; }

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
    [GeneratedRegex(@"^\s*-\s+`(?<name>[^`]+)`")]
    private static partial Regex Declaration { get; }

    /// <summary>A section's <c>**Status:**</c> bullet: its claim of how far the context exists.</summary>
    /// <remarks>
    /// Anchored on the bullet, which is what keeps the Actors table of the Training Catalogue
    /// section — whose header also says <c>Status</c> — out of the match.
    /// </remarks>
    [GeneratedRegex(@"^\s*-\s+\*\*Status:\*\*\s*(?<status>.+)$")]
    private static partial Regex StatusBullet { get; }

    /// <summary>A mermaid node label, written <c>X["&lt;b&gt;Name&lt;/b&gt;…"]</c>.</summary>
    [GeneratedRegex(@"\[""<b>(?<name>[^<]+)</b>")]
    private static partial Regex NodeLabel { get; }

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

        drawn.Keys
            .Selected("context on the map")
            .Where(context => !described.Contains(context))
            .Select(context =>
                $"context-map.md lists '{context}', and bounded-contexts.md has no " +
                $"'## Context — {context}' section for it")
            .Concat(described
                .Where(context => !drawn.ContainsKey(context))
                .Select(context =>
                    $"bounded-contexts.md describes '{context}', and it is missing from the table of " +
                    "contexts in context-map.md"))
            .ShouldHold();
    }

    /// <summary>
    /// Every context on the map, agrees with its section's status.
    /// </summary>
    /// <remarks>
    /// The map claims how far each context exists twice — a State cell in its table, a subgraph in
    /// its picture — and the section's <c>**Status:**</c> bullet claims the same thing at length.
    /// Three claims about one fact drift the moment one is edited alone, and the previous rule
    /// cannot see it: the names still agree when the states no longer do. The comparison is the one
    /// distinction the record cares about — declared as a port, or built. Measured, not assumed:
    /// written against the drifted map — Notification still 'Port only', and still drawn under the
    /// ports subgraph, after ADR 0031 had made it real — and it failed twice before the map was
    /// corrected.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0023",
        "a map that does not distinguish what is built from what is intended is worse than no map")]
    public void EveryContextOnTheMap_AgreesWithItsSectionsStatus()
    {
        var statuses = SectionStatuses();
        var states = ContextsOnTheMap();

        var stated = states
            .Where(state => statuses.ContainsKey(state.Key))
            .Select(state => (Context: state.Key, State: state.Value, Status: statuses[state.Key]))
            .ToArray();

        stated
            .Selected("context stated in both documents")
            .Where(context => ClaimsBuilt(context.State) != ClaimsBuilt(context.Status))
            .Select(context =>
                $"context-map.md's table says '{context.Context}' is '{context.State}', and its " +
                $"section in bounded-contexts.md opens its Status with '{Claim(context.Status)}'. " +
                "One of the two stopped being true")
            .Concat(ContextsDrawnAsPorts()
                .Where(context => statuses.TryGetValue(context, out var status) && ClaimsBuilt(status))
                .Select(context =>
                    $"the map still draws '{context}' under the ports subgraph, and its section in " +
                    "bounded-contexts.md says it is built. Move the node into the built subgraph"))
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

    /// <summary>
    /// The table under <c>## Contexts on this map</c>: each context with its State column.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ContextsOnTheMap()
    {
        var listed = new Dictionary<string, string>(StringComparer.Ordinal);
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

            var cells = line.Split('|', StringSplitOptions.None);
            var first = cells[1].Trim();

            // The header row and the separator under it are part of the table's syntax, not of it.
            if (first.Length == 0 || first == "Context" || first.All(character => character == '-'))
            {
                continue;
            }

            listed[first] = cells.Length > 3 ? cells[3].Trim() : string.Empty;
        }

        return listed;
    }

    /// <summary>The <c>**Status:**</c> bullet of every section that carries one.</summary>
    /// <remarks>
    /// Not every section has one — the built core contexts speak through their seams instead — so
    /// the comparison runs over the contexts that state themselves in both documents.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> SectionStatuses()
    {
        var statuses = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (context, section) in BoundedContexts())
        {
            var bullet = section.Split('\n')
                .Select(line => StatusBullet.Match(line.TrimEnd('\r')))
                .FirstOrDefault(match => match.Success);

            if (bullet is not null)
            {
                statuses[context] = bullet.Groups["status"].Value;
            }
        }

        return statuses;
    }

    /// <summary>The contexts the map's picture draws inside the <c>ports</c> subgraph.</summary>
    private static IEnumerable<string> ContextsDrawnAsPorts()
    {
        var inside = false;

        foreach (var line in Lines("context-map.md"))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("subgraph ", StringComparison.Ordinal))
            {
                inside = trimmed.StartsWith("subgraph ports", StringComparison.Ordinal);
                continue;
            }

            if (trimmed == "end")
            {
                inside = false;
                continue;
            }

            var label = inside ? NodeLabel.Match(line) : Match.Empty;

            if (label.Success)
            {
                yield return label.Groups["name"].Value.Replace("&amp;", "&", StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// Whether a claim says the context is built, whatever word it uses for it.
    /// </summary>
    /// <remarks>
    /// The two documents never promised one vocabulary: the map writes 'Built', the sections open
    /// with 'real' or 'implemented'. What they did promise is one distinction — a port awaiting an
    /// adapter, or a thing that exists — and that is the only reading this rule takes a side on.
    /// </remarks>
    private static bool ClaimsBuilt(string claim) =>
        !claim.StartsWith("port only", StringComparison.OrdinalIgnoreCase)
        && !claim.StartsWith("announced", StringComparison.OrdinalIgnoreCase);

    /// <summary>The opening claim of a Status bullet, without the prose that follows it.</summary>
    private static string Claim(string status)
    {
        var period = status.IndexOf('.', StringComparison.Ordinal);

        return period < 0 ? status : status[..period];
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
