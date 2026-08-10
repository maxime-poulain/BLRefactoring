using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The developer's local overrides file: loaded by every host, shipped by nothing.
/// </summary>
/// <remarks>
/// ADR 0035 gives each developer one git-ignored <c>appsettings.Local.json</c>, loaded after
/// every other source by all three hosts. The decision has two halves and each can rot on its
/// own: a host that forgets the loading line quietly stops honoring the file, and an ignore
/// entry that goes missing turns the file from a private override into a published secret. Both
/// rules read the artifacts the decision is about — the composition roots and the ignore files —
/// with comment lines excluded before matching, the lesson <c>LoggingRules</c> measured.
/// </remarks>
public sealed class ConfigurationRules
{
    /// <summary>The composition roots the decision is about — both APIs and the Blazor BFF.</summary>
    private static readonly string[] HostPrograms =
    [
        Path.Combine("src", "DDD", "Api", "Program.cs"),
        Path.Combine("src", "DDDWithCqrs", "Api", "Program.cs"),
        Path.Combine("src", "Web", "TrainingHub.Blazor", "TrainingHub.Blazor", "Program.cs"),
    ];

    /// <summary>
    /// Every host, loads the local overrides file.
    /// </summary>
    /// <remarks>
    /// The exact call is demanded — file name, <c>optional: true</c>, <c>reloadOnChange: true</c> —
    /// because each argument is part of the decision: the name is the convention, optional is what
    /// lets every machine without the file keep booting, and the reload behavior matches the
    /// committed sources so the local file is not the one that needs a restart.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0035",
        "every host loads appsettings.Local.json last, so a developer overrides any committed source " +
        "without editing one")]
    public void EveryHost_LoadsTheLocalOverridesFile() =>
        HostPrograms
            .Selected("host Program.cs")
            .Where(program => !SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, program))
                .Split('\n')
                .Select(line => line.TrimStart())
                .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
                .Any(line => line.Contains(
                    "AddJsonFile(\"appsettings.Local.json\", optional: true, reloadOnChange: true)",
                    StringComparison.Ordinal)))
            .Select(program =>
                $"{program} never loads appsettings.Local.json. ADR 0035 gives every developer one " +
                "overrides file that works on all three hosts — add the call after CreateBuilder, " +
                "or record the new decision")
            .ShouldHold();

    /// <summary>
    /// The local overrides file, never leaves the machine.
    /// </summary>
    /// <remarks>
    /// The half of ADR 0035 most worth defending: the loading line makes the file useful, these
    /// two entries make it safe. The git pattern is unanchored, so it covers a file in any host's
    /// directory; the Docker pattern needs the explicit glob, because a dockerignore line matches
    /// from the context root.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0035",
        "the local overrides file never leaves the machine: git refuses to version it and the Docker " +
        "build context excludes it")]
    public void TheLocalOverridesFile_NeverLeavesTheMachine() =>
        new[]
        {
            (File: ".gitignore", Entry: "appsettings.Local.json"),
            (File: ".dockerignore", Entry: "**/appsettings.Local.json"),
        }
            .Selected("ignore file")
            .Where(pair => !SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, pair.File))
                .Split('\n')
                .Select(line => line.Trim())
                .Contains(pair.Entry, StringComparer.Ordinal))
            .Select(pair =>
                $"{pair.File} does not list {pair.Entry}. Without that entry a developer's local " +
                "secrets are one git add or docker build away from leaving the machine")
            .ShouldHold();
}
