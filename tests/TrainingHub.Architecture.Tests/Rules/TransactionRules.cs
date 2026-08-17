using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The two commands that write to two contexts, and what holds each together.
/// </summary>
/// <remarks>
/// Registration creates an Identity account and a <c>Trainer</c>; erasure removes them both.
/// ADR 0040 records that a registration is one transaction, ADR 0085 that its mirror is too, and
/// both rest on the condition the last rule in this file holds: the two contexts bind the same
/// connection string, so the ambient transaction stays local and nothing is promoted to a
/// coordinator that .NET does not carry outside Windows. A rule can hold the wiring; what the
/// behavior is worth is held by the facts in the shared TestKit, since a scope that rolls nothing
/// back compiles perfectly.
/// </remarks>
public sealed partial class TransactionRules
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

    /// <summary>
    /// The erasure, runs in one ambient transaction.
    /// </summary>
    /// <remarks>
    /// Registration's rule above greps the file; this one greps the member, because the two flows
    /// share the file and a file-level search would let registration's scope vouch for an erasure
    /// that opens none. The member is found by the action's declaration — its attributes fall on
    /// the previous chunk of the split, so the route literal cannot anchor it — and a rename does
    /// not slip through: a selection that finds nothing fails loudly rather than passing empty.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0085",
        "erasure is one transaction: the trainer, their trainings and the Identity account leave " +
        "together, or nobody does")]
    public void TheErasure_RunsInOneAmbientTransaction() =>
        ErasureMembers()
            .Selected("member publishing the erasure endpoint")
            .SelectMany(member => new[]
            {
                ("new TransactionScope(", "opens no transaction scope, so a failure after the " +
                    "trainer is staged could erase the catalog and keep the account"),
                (".Complete()", "never completes the scope, so a successful erasure rolls " +
                    "itself back"),
                ("userManager.DeleteAsync", "never deletes the account, so an erased trainer " +
                    "leaves an account that signs in over nothing"),
            }
                .Where(part => !member.Contains(part.Item1, StringComparison.Ordinal))
                .Select(part => $"{Registration}'s erasure member {part.Item2}"))
            .ShouldHold();

    /// <summary>
    /// The erasure, demands the caller's own password.
    /// </summary>
    /// <remarks>
    /// A session is not proof of intent: an access token outlives an unlocked device by up to
    /// sixty minutes, and erasure is the one action this API cannot undo for anyone. The password
    /// check is the difference between "whoever holds the tab" and "whoever owns the account",
    /// and it is the kind of guard a refactoring removes politely — one call, no test of its own
    /// unless this rule is it (ADR 0085).
    /// </remarks>
    [Fact]
    [ArchitectureRule("0085",
        "erasing the account demands the caller's current password: a stolen sixty-minute " +
        "session must not be enough to destroy an account")]
    public void TheErasure_DemandsTheCallersOwnPassword() =>
        ErasureMembers()
            .Selected("member publishing the erasure endpoint")
            .Where(member => !member.Contains("CheckPasswordAsync", StringComparison.Ordinal))
            .Select(_ =>
                $"{Registration}'s erasure member never calls CheckPasswordAsync, so any live " +
                "session can erase the account without proving it belongs to the person holding it")
            .ShouldHold();

    // Where a member begins: four spaces, then an access modifier — LifecycleRules' split, for
    // the same reason it works there: file-scoped namespaces and Allman braces make the anchor
    // exact, so a file becomes members without a parser.
    [GeneratedRegex(@"^    (?=(public|private|protected|internal)\s)", RegexOptions.Multiline)]
    private static partial Regex MemberBoundary { get; }

    /// <summary>The members of the shared auth controller that publish the erasure endpoint.</summary>
    private static IEnumerable<string> ErasureMembers() =>
        MemberBoundary
            .Split(SourceTree.ReadText(Path.Combine(
                SourceTree.RepositoryRoot,
                Registration.Replace('/', Path.DirectorySeparatorChar))))
            .Where(member => member.Contains("Task<IActionResult> EraseAccount(", StringComparison.Ordinal));

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
