using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The reset credential of ADR 0084, held to the three properties that make it safe: nothing but
/// a digest at rest, one credential per account, and a life of exactly fifteen minutes.
/// </summary>
/// <remarks>
/// Each rule pins the shape in the schema or the letter in the source, because each property is
/// the kind that erodes one convenient refactoring at a time — a raw-token column added "for
/// debugging", a surrogate key that quietly allows a second live link, a lifetime widened during
/// a demo and never narrowed back. What the flow is <em>worth</em> is held by the shared TestKit
/// facts; these rules hold what the flow is made of.
/// </remarks>
public sealed class PasswordRecoveryRules
{
    private static readonly string Store = Path.Combine(
        "src", "TrainingHub.Shared.Infrastructure", "ThirdParty", "Identity", "PasswordResetTokenStore.cs");

    private static readonly string Configuration = Path.Combine(
        "src", "TrainingHub.Shared.Infrastructure", "ThirdParty", "Identity", "PasswordResetTokenConfiguration.cs");

    private static readonly string Entity = Path.Combine(
        "src", "TrainingHub.Shared.Infrastructure", "ThirdParty", "Identity", "PasswordResetToken.cs");

    /// <summary>
    /// The reset credential, is stored only digested.
    /// </summary>
    /// <remarks>
    /// Two halves that fail apart. The entity's property census refuses any column beyond the
    /// four the record names — in particular any <see langword="string"/>, which is the shape a
    /// raw token would arrive in. And the store must keep the three calls that make the token
    /// worth digesting: minted from <c>RandomNumberGenerator</c>, digested with
    /// <c>SHA256.HashData</c>, compared with <c>FixedTimeEquals</c> — entropy, one-way storage,
    /// and a comparison that leaks no timing, each one a decision of ADR 0084.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0084",
        "the reset token is 256 bits from the system's entropy and only its SHA-256 digest is " +
        "ever stored, so a copy of the database redeems nothing")]
    public void TheResetCredential_IsStoredOnlyDigested()
    {
        var expected = new[]
        {
            nameof(PasswordResetToken.UserId),
            nameof(PasswordResetToken.TokenHash),
            nameof(PasswordResetToken.CreatedOnUtc),
            nameof(PasswordResetToken.ExpiresOnUtc),
        };

        var properties = typeof(PasswordResetToken).GetProperties();
        var store = Code(Store);

        properties
            .Selected("property the reset credential's row declares")
            .Where(property => !expected.Contains(property.Name, StringComparer.Ordinal)
                || property.PropertyType == typeof(string))
            .Select(property =>
                $"PasswordResetToken.{property.Name} is not one of the four columns ADR 0084 " +
                "names, or is a string — the shape a raw token would be smuggled in as")
            .Concat(new[] { "RandomNumberGenerator", "SHA256.HashData", "FixedTimeEquals" }
                .Where(call => !Array.Exists(store, line => line.Contains(call, StringComparison.Ordinal)))
                .Select(call =>
                    $"{Store} no longer calls {call}, which is one of the three cryptographic " +
                    "decisions ADR 0084 records — entropy, digest-at-rest, fixed-time comparison"))
            .ShouldHold();
    }

    /// <summary>
    /// One reset credential, per account — as schema, not as discipline.
    /// </summary>
    [Fact]
    [ArchitectureRule("0084",
        "the account is the credential row's primary key, so a second live reset link for the " +
        "same account is unrepresentable and issuing replaces rather than adds")]
    public void OneResetCredential_PerAccount() =>
        new[] { Configuration }
            .Selected("file mapping the reset credential")
            .Where(file => !Array.Exists(
                Code(file),
                line => line.Contains("HasKey(token => token.UserId)", StringComparison.Ordinal)))
            .Select(file =>
                $"{file} no longer keys the credential by its account. The primary key on UserId " +
                "is the latest-only invariant of ADR 0084 stated as schema; a surrogate key would " +
                "turn it back into a discipline")
            .ShouldHold();

    /// <summary>
    /// The reset credential, dies in fifteen minutes.
    /// </summary>
    /// <remarks>
    /// The literal is pinned where every mention of the window reads it —
    /// <see cref="PasswordResetToken.Lifetime"/> — so the store's stamp, the email's sentence and
    /// the page's copy cannot drift apart without this rule naming the file that moved.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0084",
        "a reset link is valid for exactly fifteen minutes: long enough to walk to an inbox, " +
        "short enough that a forwarded or leaked email goes stale before it goes around")]
    public void TheResetCredential_DiesInFifteenMinutes() =>
        new[] { Entity }
            .Selected("file declaring the credential's lifetime")
            .Where(file => !Array.Exists(
                Code(file),
                line => line.Contains("TimeSpan.FromMinutes(15)", StringComparison.Ordinal)))
            .Select(file =>
                $"{file} no longer declares the fifteen-minute lifetime ADR 0084 records. The " +
                "window is a requirement, not a knob, which is why it is a constant this rule " +
                "can read rather than configuration it cannot")
            .ShouldHold();

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
