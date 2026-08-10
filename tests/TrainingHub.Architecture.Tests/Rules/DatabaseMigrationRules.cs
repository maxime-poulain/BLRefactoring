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
