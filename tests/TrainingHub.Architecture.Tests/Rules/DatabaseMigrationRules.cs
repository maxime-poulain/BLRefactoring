using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// Where a schema may be applied from, and where it may only be reported on.
/// </summary>
/// <remarks>
/// ADR 0003 lets a host migrate its own database in Development and forbids it everywhere else,
/// because two instances starting together migrate concurrently, the application would need
/// standing DDL rights, and a schema change that a process makes on startup cannot be undone by
/// stopping it. The record was excused from having a rule on the grounds that a type-level rule
/// can see the branch exists but not which way it goes — true of reflection, and not of reading
/// the file, which is what the suite learned to do for ADR 0026 and has done since. See ADR 0039.
/// </remarks>
public sealed class DatabaseMigrationRules
{
    private static readonly string Extension =
        Path.Combine("src", "TrainingHub.Shared.Api", "Extensions", "DatabaseMigrationExtensions.cs");

    private static readonly string[] ApiHostPrograms =
    [
        Path.Combine("src", "DDD", "Api", "Program.cs"),
        Path.Combine("src", "DDDWithCqrs", "Api", "Program.cs"),
    ];

    /// <summary>
    /// Migrations are applied, in development only.
    /// </summary>
    /// <remarks>
    /// Three claims, and the middle one is the decision. The applying calls sit inside the
    /// environment branch and the reporting calls sit after it, so a reader of the file cannot
    /// mistake which way the branch goes; nothing else in the solution migrates at all, so the
    /// branch is the only door; and both hosts walk through the same extension, so neither can
    /// quietly acquire a different answer. Comment lines are stripped first, the lesson
    /// <c>LoggingRules</c> measured — a commented-out call still contains its own name.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0003",
        "migrations are applied on startup in Development only; everywhere else the pending ones are reported and nothing is applied")]
    public void MigrationsAreApplied_InDevelopmentOnly()
    {
        var code = Code(Extension);

        var guard = Array.FindIndex(code, line =>
            line.Contains("if (environment.IsDevelopment())", StringComparison.Ordinal));

        var report = Array.FindIndex(code, line =>
            line.Contains("await ReportPendingMigrationsAsync<", StringComparison.Ordinal));

        var violations = new List<string>();

        if (guard < 0 || report < 0)
        {
            violations.Add(
                $"{Extension} no longer opens an 'if (environment.IsDevelopment())' branch followed " +
                "by the reporting calls. ADR 0003 is that shape — restore it, or record the new decision");
        }
        else
        {
            violations.AddRange(code
                .Select((line, index) => (line, index))
                .Where(entry => entry.line.Contains("await MigrateAsync<", StringComparison.Ordinal))
                .Where(entry => entry.index < guard || entry.index > report)
                .Select(entry =>
                    $"{Extension} applies a migration outside the Development branch, at line " +
                    $"{entry.index + 1} of its stripped source. Outside Development the schema is " +
                    "applied out of band, and this host only reports what is pending"));
        }

        violations
            .Concat(ApiHostPrograms
                .Selected("API host Program.cs")
                .Where(program => !Code(program).Any(line =>
                    line.Contains("EnsureDatabasesAreUpToDateAsync(", StringComparison.Ordinal)))
                .Select(program =>
                    $"{program} never calls EnsureDatabasesAreUpToDateAsync. Both hosts need the same " +
                    "treatment, which is why ADR 0003 puts it in one extension"))
            .Concat(OtherMigrators())
            .ShouldHold();
    }

    /// <summary>
    /// The catalog's database reads from a snapshot.
    /// </summary>
    /// <remarks>
    /// Two claims, and the second is the one a well-meaning cleanup would break. Exactly one
    /// migration turns <c>READ_COMMITTED_SNAPSHOT</c> on, so the setting has a home in the schema's
    /// history rather than in somebody's runbook; and nothing that ships turns it back off, because
    /// a reader that takes shared locks is a reader that can be a deadlock victim — which is how the
    /// public catalog came to answer 500 while an account was being erased beside it (ADR 0094).
    /// The <c>Down</c> half is exempt by name: a migration that cannot be reversed is not a
    /// migration.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0094",
        "the database reads from a snapshot, so the catalog's reader takes no shared locks and cannot be chosen as a deadlock victim")]
    public void TheCatalogsDatabase_ReadsFromASnapshot()
    {
        var migrations = SourceTree.SourceFiles
            .Select(SourceTree.Relative)
            .Where(file => file.Contains("/Migrations/", StringComparison.Ordinal))
            .Where(file => !file.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .ToArray();

        var turningItOn = migrations
            .Where(file => Code(file).Any(line =>
                line.Contains("READ_COMMITTED_SNAPSHOT ON", StringComparison.Ordinal)))
            .ToArray();

        var violations = new List<string>();

        if (turningItOn.Length != 1)
        {
            violations.Add(
                $"{turningItOn.Length} migrations turn READ_COMMITTED_SNAPSHOT on, and ADR 0094 asks " +
                "for exactly one. The setting belongs to the schema's history, where every database " +
                "the migrations reach picks it up — not to a runbook nobody reads twice");
        }

        violations
            .Concat(SourceTree.SourceFiles
                .Select(SourceTree.Relative)
                .Where(file => file.StartsWith("src/", StringComparison.Ordinal))
                .Where(file => !Array.Exists(turningItOn, migration => migration == file))
                .Where(file => Code(file).Any(line =>
                    line.Contains("READ_COMMITTED_SNAPSHOT OFF", StringComparison.Ordinal)))
                .Select(file =>
                    $"'{file}' turns READ_COMMITTED_SNAPSHOT off. Under READ COMMITTED the catalog's " +
                    "reader takes shared locks, and SQL Server answers a lock cycle by killing the " +
                    "cheaper transaction — which is the anonymous read (ADR 0094)"))
            .ShouldHold();
    }

    /// <summary>Anything outside the extension that applies a schema on its own.</summary>
    private static IEnumerable<string> OtherMigrators() =>
        SourceTree.SourceFiles
            .Select(SourceTree.Relative)
            .Where(file => file.StartsWith("src/", StringComparison.Ordinal))
            .Where(file => !file.EndsWith("DatabaseMigrationExtensions.cs", StringComparison.Ordinal))
            .Where(file => !SourceTree.IsGenerated(Path.Combine(SourceTree.RepositoryRoot, file)))
            .Where(file => Code(file).Any(line =>
                line.Contains(".Migrate(", StringComparison.Ordinal)
                || line.Contains(".MigrateAsync(", StringComparison.Ordinal)
                || line.Contains("EnsureCreated", StringComparison.Ordinal)))
            .Select(file =>
                $"'{file}' applies a schema of its own. ADR 0003 keeps that in one extension, behind " +
                "one environment branch — a second door is a second answer to the same question");

    /// <summary>A file's lines, trimmed and stripped of whole-line comments.</summary>
    private static string[] Code(string relativePath) =>
    [
        .. SourceTree
            .ReadText(Path.Combine(SourceTree.RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)))
            .Split('\n')
            .Select(line => line.TrimStart())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal)
                && !line.StartsWith("///", StringComparison.Ordinal))
    ];
}
