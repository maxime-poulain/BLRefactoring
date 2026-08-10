using TrainingHub.Architecture.Tests.Framework;
using NetArchTest.Rules;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The logging pipeline: one configuration, both hosts, one owner for the library.
/// </summary>
/// <remarks>
/// ADR 0026 puts Serilog behind a pair of extensions in <c>TrainingHub.Shared.Api</c> so the two
/// hosts cannot drift — which is a promise about two <c>Program.cs</c> files, and nothing about
/// host symmetry was checked before this. A host that quietly drops the call keeps compiling,
/// keeps passing its suite, and logs to a console that dies with the process; the drift only
/// surfaces the day its logs are needed and are not there.
/// </remarks>
public sealed class LoggingRules
{
    /// <summary>The composition roots the decision is about, by host.</summary>
    private static readonly string[] ApiHostPrograms =
    [
        Path.Combine("src", "DDD", "Api", "Program.cs"),
        Path.Combine("src", "DDDWithCqrs", "Api", "Program.cs"),
    ];

    /// <summary>
    /// Both api hosts, configure the same logging.
    /// </summary>
    /// <remarks>
    /// Read from the source rather than the compiled hosts, like the strategic-design rules: a
    /// call in <c>Program.cs</c> is top-level-statement code that reflection cannot see, and the
    /// file is the very artifact the decision is about. Comment lines are excluded before
    /// searching, and that is the difference between a rule and a formality — measured, not
    /// assumed: the first version of this rule passed against a host whose <c>UseApiLogging()</c>
    /// had been commented out, because the comment still contained the name. Both halves are
    /// demanded — registering the pipeline without the request-logging middleware silences
    /// ordinary traffic entirely, since the defaults override the framework's own narration down
    /// to Warning.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0026",
        "the two hosts share one logging pipeline: each Program.cs calls AddApiLogging and UseApiLogging")]
    public void BothApiHosts_ConfigureTheSameLogging() =>
        ApiHostPrograms
            .Selected("API host Program.cs")
            .SelectMany(program =>
            {
                var code = SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, program))
                    .Split('\n')
                    .Select(line => line.TrimStart())
                    .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
                    .ToArray();

                return new[] { "AddApiLogging", "UseApiLogging" }
                    .Where(call => !Array.Exists(code, line => line.Contains(call + "(", StringComparison.Ordinal)))
                    .Select(call =>
                        $"{program} never calls {call}. ADR 0026 wires logging once in Shared.Api " +
                        "precisely so neither host can log less than the other — add the call, or " +
                        "record the new decision");
            })
            .ShouldHold();

    /// <summary>
    /// Every log line, names its caller.
    /// </summary>
    /// <remarks>
    /// ADR 0027 was excused from having a rule because the decision is a runtime behavior — a
    /// property enriched at write time, rendered by a template — and no type-level rule can see
    /// either. What a rule can see is that the two halves are still wired to each other, which is
    /// how this breaks: a template edited without the token renders every line without its caller,
    /// and an enricher left unregistered writes a property no sink asks for. Neither fails a test
    /// that does not read the rendered output, and <c>LoggingTest</c> — which does — needs a host
    /// and a file sink. The token is compared against the enricher's own constant rather than
    /// spelled twice. See ADR 0039.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0027",
        "every line names its caller: the enricher writes the property and the one output template renders it")]
    public void EveryLogLine_NamesItsCaller()
    {
        var extension = Path.Combine("src", "TrainingHub.Shared.Api", "Extensions", "LoggingExtensions.cs");

        var code = SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, extension))
            .Split('\n')
            .Select(line => line.TrimStart())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
            .ToArray();

        var property = (string)Solution.SharedApi
            .DeclaredTypes()
            .Single(type => type.Name == "UserIdentityEnricher")
            .GetField("PropertyName")!
            .GetValue(null)!;

        new[]
        {
            ($"{{{property}}}", $"the output template no longer renders {{{property}}}, so no line " +
                "says who called — a text sink shows a property only when the template names it"),
            ("new UserIdentityEnricher(", "the enricher is never constructed, so the property it " +
                "writes exists nowhere for the template to render"),
        }
            .Selected("half of the identity stamp")
            .Where(half => !Array.Exists(code, line => line.Contains(half.Item1, StringComparison.Ordinal)))
            .Select(half => $"{extension}: {half.Item2}")
            .ShouldHold();
    }

    /// <summary>
    /// Only the logging extension, touches serilog.
    /// </summary>
    /// <remarks>
    /// The same shape as the kernel's Mediator rule: a library adopted on purpose, confined to
    /// the seam that adopts it — the <c>Logging</c> namespace that holds the options and the
    /// enricher, and the extension that wires them. The hosts consume logging through
    /// <c>AddApiLogging</c> alone, so a controller taking Serilog's <c>ILogger</c> — or a domain
    /// type reaching for the library — is a new coupling decision, not a convenience.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0026",
        "Serilog is a detail of the shared API layer: its Logging seam and extension name the library, nothing else does")]
    public void OnlyTheLoggingExtension_TouchesSerilog()
    {
        // One assembly at a time, like the kernel-purity rules: a failure names the layer that
        // broke the line instead of a merged set the reader has to sort out.
        foreach (var assembly in new[]
        {
            Solution.Kernel, Solution.Domain, Solution.Application,
            Solution.LayeredApplication, Solution.CqrsApplication,
            Solution.Infrastructure, Solution.CqrsInfrastructure,
            Solution.LayeredApi, Solution.CqrsApi
        })
        {
            Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOnAny("Serilog")
                .GetResult()
                .ShouldHold();
        }

        Types.InAssembly(Solution.SharedApi)
            .That()
            .DoNotResideInNamespace("TrainingHub.Shared.Api.Logging")
            .And()
            .DoNotHaveName("LoggingExtensions")
            .Should()
            .NotHaveDependencyOnAny("Serilog")
            .GetResult()
            .ShouldHold();
    }
}
