using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Traceability;

/// <summary>
/// The suite, held to the two things it asks of everything else.
/// </summary>
/// <remarks>
/// A suite that checks the repository and not itself has one blind spot, and it is the one that
/// matters: the coverage rule reads attributes, so a rule carrying none is invisible to it. A record
/// could then look defended by a test that never names it.
/// </remarks>
public sealed class SuiteSelfRules
{
    /// <summary>
    /// Every rule, names what it defends.
    /// </summary>
    [Fact]
    [ArchitectureRule("0013",
        "every rule names the decision it defends, or the coverage rule cannot see it")]
    public void EveryRule_NamesWhatItDefends() =>
        RuleIndex.AllTests
            .Selected("test in this suite")
            .Where(test => !RuleIndex.Rules.Any(rule => rule.Method == test))
            .Select(test =>
                $"{test.DeclaringType!.FullName}.{test.Name} carries no [ArchitectureRule]. It runs, it " +
                "passes, and it counts towards defending nothing")
            .ShouldHold();

    /// <summary>
    /// No rule of this suite, is selected by the slow workflow.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#continuous-integration",
        "the two workflow filters are exact inverses, so between them every test runs exactly once")]
    public void NoRuleOfThisSuite_IsSelectedByTheSlowWorkflow() =>
        // Not hypothetical here. Solution.cs names types from TrainingHub.DDD.Api.IntegrationTests
        // and its CQRS twin, because the kit-parity rule has to look at them. The filter reads a
        // test's own fully-qualified name and not its dependencies — which is exactly the sort of
        // thing worth proving rather than assuming.
        RuleIndex.AllTests
            .Selected("test in this suite")
            .Where(test => $"{test.DeclaringType!.FullName}.{test.Name}"
                .Contains("IntegrationTests", StringComparison.Ordinal))
            .Select(test =>
                $"{test.DeclaringType!.FullName}.{test.Name} would be selected by the integration " +
                "workflow and skipped by the fast one, which is the opposite of where it belongs")
            .ShouldHold();

    /// <summary>
    /// Every rule, quotes the decision it defends.
    /// </summary>
    [Fact]
    [ArchitectureRule("0013",
        "a rule states what it defends in the record's own words, not in a number nobody can read")]
    public void EveryRule_QuotesTheDecisionItDefends() =>
        RuleIndex.Rules
            .Selected("rule")
            .Where(rule => rule.Declaration.Rule.Length < 20)
            .Select(rule =>
                $"{rule.Name} defends {rule.Declaration.Record} with '{rule.Declaration.Rule}', which " +
                "is too short to tell a reader what broke. The sentence ends up in the failure message")
            .ShouldHold();
}
