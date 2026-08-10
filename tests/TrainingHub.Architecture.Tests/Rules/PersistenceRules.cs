using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// How a value object reaches its table, and at what precision a moment does.
/// </summary>
/// <remarks>
/// ADR 0032 decides the mapping by shape: one scalar converts, several scalars flatten as a
/// complex property, a collection owns a side table. These rules hold the two halves a scan can
/// check — nothing flattened is owned, nothing collected is JSON. The scan reads the hand-written
/// configurations only: a migration designer file records what the model was, not what it is, and
/// all of them live in <c>Migrations/</c>, beside the folder scanned here rather than inside it.
/// </remarks>
public sealed partial class PersistenceRules
{
    /// <summary>A property being configured, and the member it names.</summary>
    [GeneratedRegex(@"\.Property\(\s*(?<parameter>\w+)\s*=>\s*\k<parameter>\.(?<member>\w+)\s*\)")]
    private static partial Regex ConfiguredProperty { get; }

    /// <summary>
    /// Every moment the model stores, is stored at full precision.
    /// </summary>
    /// <remarks>
    /// ADR 0005 was excused from having a rule on the grounds that what makes it true is a value
    /// surviving a round trip — a question for a database. That half is still
    /// <c>TimestampPrecisionTest</c>'s. The other half is two literals in a hand-written
    /// configuration, and it is the half that broke once: the columns were <c>datetime2(2)</c>,
    /// buckets of ten milliseconds, which silently undid an interceptor reading the clock per
    /// entity. The scan covers every hand-written configuration under <c>EfCore/</c>, not only the
    /// <c>Configurations/</c> folder — the outbox's own timestamps carry the same decision, cite
    /// the same record, and sat outside the older scan. See ADR 0039.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0005",
        "a stored moment keeps every tick it was written with: each audit column declares its precision rather than inheriting one")]
    public void EveryMomentTheModelStores_IsStoredAtFullPrecision() =>
        Configurations()
            .Selected("EF Core configuration file")
            .SelectMany(path => Moments(SourceTree.ReadText(path))
                .Select(moment => (Path: SourceTree.Relative(path), Moment: moment)))
            .Where(entry => !entry.Moment.Declaration.Contains("HasPrecision(7)", StringComparison.Ordinal))
            .Select(entry =>
                $"'{entry.Path}' configures {entry.Moment.Member} without HasPrecision(7). A default " +
                "precision is what let datetime2(2) round two saves onto one value, and nothing said so")
            .ShouldHold();

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

    /// <summary>
    /// Every timestamp a configuration declares, with the text that configures it — up to the next
    /// property, which is where its own fluent chain ends.
    /// </summary>
    private static IEnumerable<(string Member, string Declaration)> Moments(string text)
    {
        var properties = ConfiguredProperty.Matches(text).ToArray();

        for (var index = 0; index < properties.Length; index++)
        {
            var member = properties[index].Groups["member"].Value;

            if (!IsAMoment(member))
            {
                continue;
            }

            var start = properties[index].Index;
            var end = index + 1 < properties.Length ? properties[index + 1].Index : text.Length;

            yield return (member, text[start..end]);
        }
    }

    /// <summary>
    /// The uniqueness the model cannot express, is still created by a migration.
    /// </summary>
    /// <remarks>
    /// A rule that exists because a guarantee moved out of the model and into the database, and
    /// nothing fast was left watching it. "A trainer cannot have two trainings with the same title"
    /// is enforced by a unique index on <c>(TrainerId, Title)</c>; EF Core 10 cannot declare an
    /// index over a scalar nested in a complex type, and the title became one so that a substring
    /// match would translate (ADR 0060). So <c>TrainingConfiguration</c> stopped describing the
    /// index, and only the migration creates it.
    /// <para>
    /// What that costs is a hole a refactoring falls into silently: delete the <c>CreateIndex</c>
    /// while squashing history, or write a <c>Down</c> nobody re-applies, and the pre-check in
    /// <c>IUniquenessTitleChecker</c> goes on answering while two concurrent creations both win.
    /// The only other defense is <c>Create_DuplicateTitleForSameTrainer_Returns409</c>, which runs
    /// in the integration workflow the fast one excludes.
    /// </para>
    /// <para>
    /// Read off the migrations' source rather than the model, which is the whole point: the model
    /// is where the claim no longer is.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0060",
        "a unique index the model cannot declare is still created by a migration, or the rule it " +
        "enforces holds nowhere")]
    public void TheUniquenessTheModelCannotExpress_IsStillCreatedByAMigration() =>
        Migrations()
            .Selected("migration")
            .Where(CreatesTheUniqueTitleIndex)
            .Select(_ => (string?)null)
            .DefaultIfEmpty(
                "no migration creates the unique index on (TrainerId, Title). Since ADR 0060 the " +
                "model cannot declare it — EF Core 10 refuses an index over a complex type's " +
                "member — so the migration is the only place that rule exists. Without it two " +
                "concurrent creations of the same title both succeed, and the pre-check in " +
                "IUniquenessTitleChecker answers for a race it cannot see")
            .OfType<string>()
            .ShouldHold();

    /// <summary>
    /// Whether a migration's source creates the unique index the title rule rests on.
    /// </summary>
    /// <remarks>
    /// Matched on the two column names and the uniqueness together, in one <c>CreateIndex</c> call:
    /// an index over the same pair that had lost <c>unique: true</c> would read almost identically
    /// and enforce nothing.
    /// </remarks>
    private static bool CreatesTheUniqueTitleIndex(string path)
    {
        var text = SourceTree.ReadText(path);

        var index = text.IndexOf("\"TrainerId\", \"Title\"", StringComparison.Ordinal);

        return index >= 0
               && text.IndexOf("unique: true", index, StringComparison.Ordinal) >= 0;
    }

    private static IEnumerable<string> Migrations() =>
        SourceTree.SourceFiles
            .Select(SourceTree.Relative)
            .Where(path => path.StartsWith(
                "src/TrainingHub.Shared.Infrastructure/ThirdParty/EfCore/Migrations/",
                StringComparison.Ordinal))
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Select(path => Path.Combine(SourceTree.RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>
    /// Whether a member name is one of the moments this model stores: the audit pair every
    /// aggregate carries, and the outbox's own instants.
    /// </summary>
    private static bool IsAMoment(string member) =>
        member is "CreatedOn" or "ModifiedOn"
        || member.EndsWith("OnUtc", StringComparison.Ordinal)
        || member.EndsWith("Until", StringComparison.Ordinal);

    private static IEnumerable<string> Configurations() =>
        SourceTree.SourceFiles
            .Select(SourceTree.Relative)
            .Where(path => path.StartsWith(
                "src/TrainingHub.Shared.Infrastructure/ThirdParty/EfCore/",
                StringComparison.Ordinal))
            .Where(path => path.EndsWith("Configuration.cs", StringComparison.Ordinal))
            .Select(path => Path.Combine(SourceTree.RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)));
}
