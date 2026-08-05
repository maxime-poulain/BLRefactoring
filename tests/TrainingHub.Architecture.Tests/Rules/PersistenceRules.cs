using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// How a value object reaches its table.
/// </summary>
/// <remarks>
/// ADR 0032 decides the mapping by shape: one scalar converts, several scalars flatten as a
/// complex property, a collection owns a side table. These rules hold the two halves a scan can
/// check — nothing flattened is owned, nothing collected is JSON. The scan reads the hand-written
/// configurations only: a migration designer file records what the model was, not what it is, and
/// all of them live in <c>Migrations/</c>, beside the folder scanned here rather than inside it.
/// </remarks>
public sealed class PersistenceRules
{
    /// <summary>
    /// No configuration, flattens a value object as an owned type.
    /// </summary>
    [Fact]
    [ArchitectureRule("0032",
        "a value object flattened into its owner's table is a complex property, never an owned entity")]
    public void NoConfiguration_FlattensAValueObjectAsAnOwnedType() =>
        Configurations()
            .Selected("EF Core configuration file")
            .Where(path => SourceTree.ReadText(path).Contains("OwnsOne(", StringComparison.Ordinal))
            .Select(path =>
                $"'{SourceTree.Relative(path)}' maps a value object with OwnsOne, handing it a shadow " +
                "key and a tracked identity it does not have. ADR 0032 flattens it as a complex property")
            .ShouldHold();

    /// <summary>
    /// Every value object collection, lives in a relational side table.
    /// </summary>
    [Fact]
    [ArchitectureRule("0032",
        "a collection of value objects lives in a relational side table, never in a JSON column")]
    public void EveryValueObjectCollection_LivesInARelationalSideTable() =>
        Configurations()
            .Selected("EF Core configuration file")
            .Select(path => (Path: SourceTree.Relative(path), Text: SourceTree.ReadText(path)))
            .Where(file => OwnsACollectionWithoutNamingItsTable(file.Text)
                           || file.Text.Contains("ToJson(", StringComparison.Ordinal))
            .Select(file =>
                $"'{file.Path}' stores a value object collection without naming its side table, or " +
                "sends one to a JSON column. ADR 0032 keeps collections relational, where their " +
                "columns stay typed, bounded and indexable")
            .ShouldHold();

    /// <summary>
    /// Whether any <c>OwnsMany</c> in the text reaches the next one — or the end — without a
    /// <c>ToTable</c>, which is what leaves the collection's table to convention.
    /// </summary>
    private static bool OwnsACollectionWithoutNamingItsTable(string text)
    {
        for (var index = text.IndexOf("OwnsMany(", StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf("OwnsMany(", index + 1, StringComparison.Ordinal))
        {
            var next = text.IndexOf("OwnsMany(", index + 1, StringComparison.Ordinal);
            var end = next < 0 ? text.Length : next;

            if (text.IndexOf("ToTable(", index, end - index, StringComparison.Ordinal) < 0)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> Configurations() =>
        SourceTree.SourceFiles
            .Where(path => SourceTree.Relative(path).StartsWith(
                "src/TrainingHub.Shared.Infrastructure/ThirdParty/EfCore/Configurations/",
                StringComparison.Ordinal));
}
