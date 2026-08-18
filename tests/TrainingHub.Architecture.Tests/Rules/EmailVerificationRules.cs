using System.Reflection;
using AwesomeAssertions;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The verification credential and the one door it opens: what rests in the database, how it is
/// minted and spent, and where the boundary's courtesy sits.
/// </summary>
/// <remarks>
/// ADR 0090's decisions as rules, in ADR 0084's molds. The credential's schema and crypto are
/// pinned at the source because they are the security posture — a column gaining a raw token, a
/// digest downgrading, or a delete losing its guard would each pass every behavior test that
/// matters until the day it mattered. The Identity flags are pinned because a one-line
/// "hardening" would silently invert the business rule: an unverified account must sign in.
/// </remarks>
public sealed class EmailVerificationRules
{
    /// <summary>Where the credential's store is written.</summary>
    private const string TokenStoreFile =
        "src/TrainingHub.Shared.Infrastructure/ThirdParty/Identity/EmailVerificationTokenStore.cs";

    /// <summary>Where the credential's mapping is written.</summary>
    private const string TokenConfigurationFile =
        "src/TrainingHub.Shared.Infrastructure/ThirdParty/Identity/EmailVerificationTokenConfiguration.cs";

    /// <summary>Where the Identity options are composed, for both hosts at once.</summary>
    private const string IdentityExtensionsFile =
        "src/TrainingHub.Shared.Api/Extensions/IdentityExtensions.cs";

    /// <summary>
    /// The verification credential, is three columns and no string.
    /// </summary>
    /// <remarks>
    /// The census is exact in both directions, and the type constraint is the half that bites: a
    /// raw token is a string, a digest is bytes, so a property of type <see cref="string"/>
    /// appearing on this entity is the leak ADR 0090's at-rest posture forbids — whatever the
    /// property is named.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0090",
        "at rest the credential is a digest beside an account and an instant — three columns, " +
        "none of them a string a raw token could hide in")]
    public void TheVerificationCredential_IsThreeColumnsAndNoString()
    {
        var entity = Solution.Infrastructure.DeclaredTypes()
            .Single(type => type.Name == "EmailVerificationToken");

        var properties = entity
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToList();

        properties.Select(property => property.Name)
            .Should().BeEquivalentTo(
                ["UserId", "TokenHash", "CreatedOnUtc"],
                "the row is the account, the digest and the hedge — a fourth column is a new decision");

        properties.Selected("property of the verification credential")
            .Where(property => property.PropertyType == typeof(string))
            .Select(property =>
                $"EmailVerificationToken.{property.Name} is a string. Nothing on this entity may " +
                "be one: a raw token is a string, a digest is bytes, and the type is the fence " +
                "(ADR 0090)")
            .ShouldHold();
    }

    /// <summary>
    /// The store, mints from the systems entropy and spends with one guarded delete.
    /// </summary>
    /// <remarks>
    /// Pinned at the source, like the logging and localization promises, because reflection
    /// cannot see method bodies: the entropy source, the digest algorithm and the delete's guard
    /// are each one edit away from a quiet downgrade. The guard matters most — an
    /// <c>ExecuteDeleteAsync</c> filtered on the account alone would spend whatever credential
    /// the row holds <em>now</em>, and two racing clicks would both win.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0090",
        "the token is 256 bits from RandomNumberGenerator, only its SHA-256 digest is written, " +
        "and redemption is one delete guarded by account and digest together")]
    public void TheStore_MintsFromEntropyAndSpendsWithOneGuardedDelete()
    {
        var store = SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, TokenStoreFile));

        new[]
        {
            ("RandomNumberGenerator.GetBytes", "the token's entropy is the system's, never a Guid or a clock"),
            ("SHA256.HashData", "only a digest rests — a reversible or weaker function is a new decision"),
            ("candidate.UserId == current.UserId && candidate.TokenHash == digest",
                "the delete is guarded by account and digest together, which is what settles two racing clicks"),
            ("ExecuteDeleteAsync", "consumption is the database's one atomic delete, not a read-then-remove"),
        }
            .Selected("promise the store's source must keep")
            .Where(promise => !store.Contains(promise.Item1, StringComparison.Ordinal))
            .Select(promise => $"{TokenStoreFile} no longer contains '{promise.Item1}': {promise.Item2} (ADR 0090)")
            .ShouldHold();
    }

    /// <summary>
    /// The mapping, keys the account and uniquely indexes the digest.
    /// </summary>
    /// <remarks>
    /// The primary key <em>is</em> the latest-only invariant — one row per account, enforced by
    /// the schema rather than by discipline — and the unique index is what redemption's
    /// digest-only lookup stands on (ADR 0090's recorded deviation from ADR 0084's account-keyed
    /// read). Both live in one static configuration this rule reads at the source.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0090",
        "one credential per account by primary key, and a unique index on the digest the emailed " +
        "link is redeemed by")]
    public void TheMapping_KeysTheAccountAndUniquelyIndexesTheDigest()
    {
        var configuration = SourceTree.ReadText(
            Path.Combine(SourceTree.RepositoryRoot, TokenConfigurationFile));

        new[]
        {
            ("HasKey(token => token.UserId)", "the account-keyed identity is the latest-only invariant itself"),
            ("HasIndex(token => token.TokenHash)", "redemption looks the row up by digest and nothing else"),
            (".IsUnique()", "the digest's index must be unique, or two accounts could share a credential"),
        }
            .Selected("promise the mapping's source must keep")
            .Where(promise => !configuration.Contains(promise.Item1, StringComparison.Ordinal))
            .Select(promise =>
                $"{TokenConfigurationFile} no longer contains '{promise.Item1}': {promise.Item2} (ADR 0090)")
            .ShouldHold();
    }

    /// <summary>
    /// The identity options, never demand a confirmed email to sign in.
    /// </summary>
    /// <remarks>
    /// The guard against the helpful one-liner: <c>SignIn.RequireConfirmedEmail</c> and
    /// <c>SignIn.RequireConfirmedAccount</c> each read as hardening and each inverts the business
    /// rule — an unverified account signs in, manages its space and reads everything; only the
    /// catalog write asks for the proof (ADR 0090). Comment lines are excluded before searching,
    /// so the comment that explains this absence does not trip the rule that enforces it.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0090",
        "an unverified account must sign in: the framework's confirmed-email gates are never set, " +
        "because the refusal belongs on one door and not at the front gate")]
    public void TheIdentityOptions_NeverDemandAConfirmedEmailToSignIn()
    {
        var code = SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, IdentityExtensionsFile))
            .Split('\n')
            .Select(line => line.TrimStart())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
            .ToArray();

        new[] { "RequireConfirmedEmail", "RequireConfirmedAccount" }
            .Selected("framework gate that must stay unset")
            .Where(gate => Array.Exists(code, line => line.Contains(gate, StringComparison.Ordinal)))
            .Select(gate =>
                $"{IdentityExtensionsFile} sets {gate}. That one line locks every unverified " +
                "account out of signing in — the inversion of ADR 0090's rule, which closes one " +
                "door and deliberately no other")
            .ShouldHold();
    }

    /// <summary>
    /// The verification policy, guards exactly the create action on each host.
    /// </summary>
    /// <remarks>
    /// A census in both directions, because each direction fails differently: the policy
    /// spreading to another action would refuse an unverified trainer something the record
    /// grants them, and the create action losing it would drop the courtesy 403 and let every
    /// unverified create travel to the domain to be refused there — correct, and needlessly
    /// expensive. The narrowness is the decision: creation is the only door into the catalog an
    /// unverified trainer can reach, because verification is never revoked and so they can never
    /// already own a training (ADR 0090).
    /// </remarks>
    [Fact]
    [ArchitectureRule("0090",
        "the courtesy 403 sits on creating a training and nowhere else — every other surface an " +
        "unverified trainer cannot reach, or may keep")]
    public void TheVerificationPolicy_GuardsExactlyTheCreateActionOnEachHost()
    {
        foreach (var host in Solution.Hosts)
        {
            host.DeclaredTypes()
                .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
                .SelectMany(controller => controller
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(action => action
                        .GetCustomAttributes<AuthorizeAttribute>()
                        .Any(attribute => attribute.Policy == VerifiedTrainerPolicy.Name))
                    .Select(action => $"{controller.Name}.{action.Name}"))
                .Should().BeEquivalentTo(
                    ["TrainingController.CreateTrainingAsync"],
                    $"in {host.GetName().Name} the verification policy guards the one create " +
                    "door, exactly (ADR 0090)");
        }
    }
}
