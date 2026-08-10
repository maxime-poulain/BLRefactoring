using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What the Search Indexing context keeps to itself, now that it has something to keep (ADR 0059).
/// </summary>
/// <remarks>
/// The context was a port with a fake behind it for as long as these decisions cost nothing. A read
/// model with two tables and a query surface can be taken apart in two ways that both look like
/// ordinary shortcuts — a second writer, and a reader that gives up and asks the write model — so
/// both are refused here rather than left to a reviewer.
/// </remarks>
public sealed class SearchIndexRules
{
    /// <summary>
    /// The tables the index is made of. Nothing outside its own layer knows they exist.
    /// </summary>
    private static readonly string[] IndexTables =
    [
        "TrainingSearchEntry",
        "TrainingSearchTerm",
        "TrainingSearchTopic"
    ];

    /// <summary>
    /// The layer allowed to name them: the one holding the adapter, its configuration and the
    /// migration that created them.
    /// </summary>
    private const string TheIndexsOwnLayer = "src/TrainingHub.Shared.Infrastructure/";

    /// <summary>
    /// No layer above the index, names the tables it is made of.
    /// </summary>
    /// <remarks>
    /// The published language of this context is two ports — one that writes and one that reads —
    /// and its storage is nobody else's business. The temptation is specific rather than abstract:
    /// every query handler on the CQRS host projects columns out of a <c>DbSet</c>, so the obvious
    /// way to write the catalog's reader was to open one over the index. It would have worked,
    /// and it would have made a bounded context into a table this application happens to share.
    /// <para>
    /// Read off the source, because the claim is about what a file <em>says</em>: a handler naming
    /// the entity in a generic argument leaves it in the metadata, and one naming it in a raw SQL
    /// string does not.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0059",
        "the index's storage belongs to the index: everything else reaches it through the two " +
        "ports, or not at all")]
    public void NoLayerAboveTheIndex_NamesTheTablesItIsMadeOf() =>
        SourceTree.SourceFiles
            .Select(SourceTree.Relative)
            .Where(file => file.StartsWith("src/", StringComparison.Ordinal))
            .Where(file => !file.StartsWith(TheIndexsOwnLayer, StringComparison.Ordinal))
            .Selected("source file outside the index's own layer")
            .SelectMany(file => IndexTables
                .Where(table => Text(file).Contains(table, StringComparison.Ordinal))
                .Select(table =>
                    $"'{file}' names '{table}'. The search index's storage is the Search Indexing " +
                    "context's own: what reads it is ITrainingSearchQuery and what writes it is " +
                    "ITrainingSearchIndexer, and a second door makes the two one context (ADR 0059)"))
            .ShouldHold();

    /// <summary>
    /// The two aggregate collections. Naming one is how a reader gives up on the index.
    /// </summary>
    private static readonly string[] TheWriteModel =
    [
        ".Trainings",
        ".Trainers",
        "Repository"
    ];

    /// <summary>
    /// No catalog search, reads the write model.
    /// </summary>
    /// <remarks>
    /// The point of having built an index. ADR 0055 refused this context a query surface until there
    /// was an index behind it, on the grounds that a facade over the same tables would be "building
    /// the second system to avoid a <c>LIKE</c>" — and a search path that quietly reached back into
    /// the trainings table would be exactly that, with the index left as decoration.
    /// <para>
    /// The set is found by what a file does rather than by a list of paths: asking the index its
    /// question means naming the port and calling it, so the rule covers the file that will be added
    /// tomorrow. The indexer is not among them and must not be — it reads the write model on
    /// purpose, once per fact, which is what a projection is.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0059",
        "a search that falls back on the aggregates has not been built; the index is the answer or " +
        "there is no index")]
    public void NoCatalogSearch_ReadsTheWriteModel() =>
        SourceTree.SourceFiles
            .Select(SourceTree.Relative)
            .Where(file => file.StartsWith("src/", StringComparison.Ordinal))
            .Where(file => Text(file).Contains("ITrainingSearchQuery", StringComparison.Ordinal)
                           && Text(file).Contains("SearchAsync(", StringComparison.Ordinal))
            .Selected("file asking the search index its question")
            .SelectMany(file => TheWriteModel
                .Where(term => Text(file).Contains(term, StringComparison.Ordinal))
                .Select(term =>
                    $"'{file}' names '{term}'. A catalog search reads the index and nothing else: " +
                    "reaching the aggregates from here restores the scan ADR 0055 recorded and " +
                    "leaves the index as decoration (ADR 0059)"))
            .ShouldHold();

    /// <summary>
    /// Every topic the domain declares, fits the index column.
    /// </summary>
    /// <remarks>
    /// The green failure this one refuses: a new <c>Topic</c> wider than the column would fail on
    /// the first training filed under it, at index time inside a consumer — no build breaks, and
    /// no suite that does not create precisely that training goes red. The bound is read off the
    /// configuration rather than restated, so narrowing the column without consulting the domain
    /// turns this red the same way widening a name does (ADR 0069).
    /// </remarks>
    [Fact]
    [ArchitectureRule("0069",
        "the index stores a topic by the name the domain spells, and every name the domain declares fits")]
    public void EveryTopicTheDomainDeclares_FitsTheIndexColumn()
    {
        var bound = TopicColumnBound();

        Topic.GetTopics()
            .Selected("topic the domain declares")
            .Where(topic => topic.Name.Length > bound)
            .Select(topic =>
                $"'{topic.Name}' is {topic.Name.Length} characters and the index column holds " +
                $"{bound}. The first training filed under it would fail at index time, in a " +
                "consumer, long after every build was green (ADR 0069)")
            .ShouldHold();
    }

    /// <summary>
    /// The width the topics' configuration declares, read rather than restated.
    /// </summary>
    private static int TopicColumnBound()
    {
        var configuration = Text(
            $"{TheIndexsOwnLayer}ThirdParty/EfCore/Search/TrainingSearchTopicConfiguration.cs");

        const string Marker = ".HasMaxLength(";
        var start = configuration.IndexOf(Marker, StringComparison.Ordinal);

        if (start < 0)
        {
            // Vacuity: a configuration that stopped declaring a bound would leave nvarchar(max)
            // behind — every name would fit, and this rule would be asserting nothing at all.
            throw new InvalidOperationException(
                "TrainingSearchTopicConfiguration.cs declares no HasMaxLength. The rule measures " +
                "the domain's names against that bound; with none declared, it asserts nothing.");
        }

        var value = configuration[(start + Marker.Length)..];

        return int.Parse(value[..value.IndexOf(')', StringComparison.Ordinal)],
            System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The text of a repository-relative file.</summary>
    private static string Text(string relativePath) =>
        SourceTree.ReadText(Path.Combine(
            SourceTree.RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
