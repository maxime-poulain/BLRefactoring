using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The one command that writes to two contexts, and what holds it together.
/// </summary>
/// <remarks>
/// Registration creates an Identity account and a <c>Trainer</c>. ADR 0040 records that the two
/// are one transaction, and records the condition that makes it work: both contexts bind the same
/// connection string, so the ambient transaction stays local and nothing is promoted to a
/// coordinator that .NET does not carry outside Windows. A rule can hold the wiring; what the
/// behaviour is worth is held by the fact in the shared TestKit, since a scope that rolls nothing
/// back compiles perfectly.
/// </remarks>
public sealed class TransactionRules
{
    private static readonly string Registration =
        Path.Combine("src", "TrainingHub.Shared.Api", "Controllers", "AuthControllerBase.cs");

    /// <summary>The two places a context is told where its database is.</summary>
    private static readonly string[] ContextRegistrations =
    [
        Path.Combine("src", "TrainingHub.Shared.Api", "Extensions", "IdentityExtensions.cs"),
        Path.Combine("src", "TrainingHub.Shared.Infrastructure", "Extensions", "ServiceCollectionExtensions.cs"),
    ];

    private const string ConnectionStringKey = "GetConnectionString(\"TrainingContext\")";

    /// <summary>
    /// Registration, runs in one ambient transaction.
    /// </summary>
    /// <remarks>
    /// Three literals, and each one is load-bearing. The scope is what makes the two writes one.
    /// <c>TransactionScopeAsyncFlowOption.Enabled</c> is what carries it across the awaits — without
    /// it the ambient transaction does not flow and the scope silently guards nothing, which is the
    /// failure that looks like working code. And the single connection-string key is the condition
    /// under which the transaction stays local: two different databases here would need a
    /// distributed coordinator, which is unavailable on this platform, so the drift would surface
    /// as a runtime failure rather than as a slower commit.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0040",
        "registration is one transaction: the Identity account and the trainer are both written, or neither is")]
    public void Registration_RunsInOneAmbientTransaction()
    {
        var code = Code(Registration);

        new[]
        {
            ("new TransactionScope(", "opens no transaction scope, so a refused trainer leaves the " +
                "account it was created beside"),
            ("TransactionScopeAsyncFlowOption.Enabled", "does not enable async flow, so the ambient " +
                "transaction does not survive the awaits and the scope guards nothing"),
            (".Complete()", "never completes the scope, so a successful registration rolls itself back"),
        }
            .Selected("half of the registration transaction")
            .Where(part => !Array.Exists(code, line => line.Contains(part.Item1, StringComparison.Ordinal)))
            .Select(part => $"{Registration} {part.Item2}")
            .Concat(ContextRegistrations
                .Where(registration => !Array.Exists(
                    Code(registration),
                    line => line.Contains(ConnectionStringKey, StringComparison.Ordinal)))
                .Select(registration =>
                    $"{registration} no longer binds its context to {ConnectionStringKey}. The two " +
                    "contexts share one database, which is what keeps registration's transaction " +
                    "local; a second database would ask for a coordinator this platform does not have"))
            .ShouldHold();
    }

    /// <summary>A file's lines, trimmed and stripped of whole-line comments.</summary>
    private static string[] Code(string relativePath) =>
    [
        .. SourceTree
            .ReadText(Path.Combine(SourceTree.RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)))
            .Split('\n')
            .Select(line => line.TrimStart())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
    ];
}
