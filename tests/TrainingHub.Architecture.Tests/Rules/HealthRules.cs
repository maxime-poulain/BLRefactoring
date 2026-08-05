using TrainingHub.Architecture.Tests.Framework;
using NetArchTest.Rules;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The health endpoints: every host answers for itself, and neither API host answers less than
/// the other.
/// </summary>
/// <remarks>
/// ADR 0037 wires liveness and readiness once, in <c>Shared.Api</c>, the same bargain the logging
/// pair struck in ADR 0026 — which again is a promise about <c>Program.cs</c> files that nothing
/// compiled can prove. A host that drops the call keeps building and keeps serving; the drift only
/// surfaces the day an orchestrator polls an endpoint that is not there.
/// </remarks>
public sealed class HealthRules
{
    /// <summary>The composition roots the decision is about, by host.</summary>
    private static readonly string[] ApiHostPrograms =
    [
        Path.Combine("src", "DDD", "Api", "Program.cs"),
        Path.Combine("src", "DDDWithCqrs", "Api", "Program.cs"),
    ];

    /// <summary>
    /// Both api hosts, answer for their health.
    /// </summary>
    /// <remarks>
    /// Read from the source and stripped of comment lines before searching, exactly as the
    /// logging rule learnt to: a call in <c>Program.cs</c> is top-level-statement code reflection
    /// cannot see, and a commented-out call still contains the name. Both halves are demanded —
    /// <c>AddApiHealth</c> registers the four probes, <c>MapApiHealth</c> publishes the two
    /// endpoints, and either one alone is a host that cannot answer.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0037",
        "every host answers for its own health: liveness says the process serves, readiness says its world " +
        "— database, object store, mail relay, outbox — is reachable, and the body names statuses and nothing else")]
    public void BothApiHosts_AnswerForTheirHealth() =>
        ApiHostPrograms
            .Selected("API host Program.cs")
            .SelectMany(program =>
            {
                var code = SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, program))
                    .Split('\n')
                    .Select(line => line.TrimStart())
                    .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
                    .ToArray();

                return new[] { "AddApiHealth", "MapApiHealth" }
                    .Where(call => !Array.Exists(code, line => line.Contains(call + "(", StringComparison.Ordinal)))
                    .Select(call =>
                        $"{program} never calls {call}. ADR 0037 wires health once in Shared.Api " +
                        "precisely so neither host can answer less than the other — add the call, " +
                        "or record the new decision");
            })
            .ShouldHold();

    /// <summary>
    /// The bff, answers for its liveness.
    /// </summary>
    /// <remarks>
    /// The literals differ from the API rule's on purpose: the BFF targets net9.0 and cannot
    /// reach the net10 <c>Shared.Api</c> extension, so ADR 0037 gives it the two framework calls
    /// inline — liveness only, because its world is the API and proxying a readiness answer would
    /// be a decision of its own.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0037",
        "every host answers for its own health: the BFF, out of the shared extension's reach, carries the " +
        "framework's liveness pair inline")]
    public void TheBff_AnswersForItsLiveness()
    {
        var program = Path.Combine("src", "Web", "TrainingHub.Blazor", "TrainingHub.Blazor", "Program.cs");
        var code = SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, program))
            .Split('\n')
            .Select(line => line.TrimStart())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
            .ToArray();

        new[] { "AddHealthChecks", "MapHealthChecks" }
            .Selected("framework health call")
            .Where(call => !Array.Exists(code, line => line.Contains(call + "(", StringComparison.Ordinal)))
            .Select(call =>
                $"{program} never calls {call}. ADR 0037 has every host answer for its own health, " +
                "and the BFF's share is the framework's liveness pair inline — add the call, or " +
                "record the new decision")
            .ShouldHold();
    }

    /// <summary>
    /// Both api hosts, serve the dashboard in development.
    /// </summary>
    /// <remarks>
    /// The same source scan as the endpoint rule above, for the same reason: the pair is
    /// top-level-statement code reflection cannot see. The gating is not scanned for — it lives
    /// inside the extensions, which no-op outside Development — so what the rule demands is only
    /// that both hosts make the calls, and what the extension guarantees is that production never
    /// grows a control room out of them.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0037",
        "the dashboard is a Development tool: both API hosts wire the shared pair, and production " +
        "serves statuses, never a control room")]
    public void BothApiHosts_ServeTheDashboardInDevelopment() =>
        ApiHostPrograms
            .Selected("API host Program.cs")
            .SelectMany(program =>
            {
                var code = SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, program))
                    .Split('\n')
                    .Select(line => line.TrimStart())
                    .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
                    .ToArray();

                return new[] { "AddApiHealthDashboard", "MapApiHealthDashboard" }
                    .Where(call => !Array.Exists(code, line => line.Contains(call + "(", StringComparison.Ordinal)))
                    .Select(call =>
                        $"{program} never calls {call}. ADR 0037 gives Development a dashboard on " +
                        "both hosts through one shared, self-gating pair — add the call, or record " +
                        "the new decision");
            })
            .ShouldHold();

    /// <summary>
    /// Only the health seam, touches the dashboard library.
    /// </summary>
    /// <remarks>
    /// The same shape as the Serilog rule: a library adopted on purpose, confined to the seam
    /// that adopts it — the <c>Health</c> namespace and the extension that wires it. The hosts
    /// consume the dashboard through <c>AddApiHealthDashboard</c> alone, so a controller or an
    /// inner layer naming <c>HealthChecks.UI</c> is a new coupling decision, not a convenience.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0037",
        "the dashboard library is a detail of the shared API layer: its Health seam and extension name it, nothing else does")]
    public void OnlyTheHealthSeam_TouchesTheDashboardLibrary()
    {
        // One assembly at a time, like the Serilog rule: a failure names the layer that broke
        // the line instead of a merged set the reader has to sort out.
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
                .NotHaveDependencyOnAny("HealthChecks.UI")
                .GetResult()
                .ShouldHold();
        }

        Types.InAssembly(Solution.SharedApi)
            .That()
            .DoNotResideInNamespace("TrainingHub.Shared.Api.Health")
            .And()
            .DoNotHaveName("HealthExtensions")
            .Should()
            .NotHaveDependencyOnAny("HealthChecks.UI")
            .GetResult()
            .ShouldHold();
    }
}
