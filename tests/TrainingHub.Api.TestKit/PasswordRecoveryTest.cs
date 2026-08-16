using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainingHub.Shared.Api.Contracts.Auth;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The recovery flow of ADR 0084, proven whole: the link travels by real SMTP, the credential
/// lives hashed in the real database, and every security property the record claims — one
/// answer for every address, one live link per account, one redemption per link, fifteen
/// minutes and not a second more — is a fact here rather than a sentence there.
/// </summary>
/// <remarks>
/// The email is the API of this flow, which is why nearly every fact goes through the mailbox:
/// the token exists nowhere else, and reading it out of Mailpit is exactly what the account's
/// owner does. The two facts that open a service scope instead — aging a credential, reading a
/// digest — reach for the row because what they assert lives in the schema and no response can
/// show it.
/// </remarks>
public abstract partial class PasswordRecoveryTest<TFactory>(TFactory factory)
    : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IMailboxSource, IServiceScopeSource
{
    private const string LinkSubject = "Reset your TrainingHub password";

    private const string ChangedSubject = "Your TrainingHub password was changed";

    private const string RefusedLinkSentence = "This password reset link is invalid or has expired.";

    [GeneratedRegex(@"token=(\S+)")]
    private static partial Regex TokenInLink { get; }

    /// <summary>
    /// Forgetting a password, delivers a link that changes it — and the link dies on use.
    /// </summary>
    [Fact]
    public async Task ForgettingAPassword_DeliversALinkThatChangesIt_ThroughRealSmtp()
    {
        // Arrange
        var client = Factory.CreateClient();
        var account = await RegisterAsync(client);

        // Act
        var accepted = await ForgotAsync(client, account.Email);
        var token = await EmailedTokenAsync(account.Email);
        var reset = await ResetAsync(client, account.Email, token, "a-new-password");

        // Assert
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var oldPassword = await client.PostAsJsonAsync("/Auth/login", new LoginHttpRequest
        {
            Username = account.Username,
            Password = account.Password
        });
        oldPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the password the link replaced must be gone the moment the reset answers");

        (await AuthHelper.LoginAsync(client, account.Username, "a-new-password"))
            .Should().NotBeNullOrWhiteSpace("the new password is the credential now");

        var reused = await ResetAsync(client, account.Email, token, "yet-another-password");
        reused.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a redeemed credential does not exist, so the same link buys nothing twice");

        (await Mailbox.WaitForEmailAsync(Factory, account.Email, ChangedSubject))
            .Should().Contain("password", "the owner's alarm bell follows every change (ADR 0084)");
    }

    /// <summary>
    /// An address nobody registered, is answered the same — and nothing ever leaves.
    /// </summary>
    /// <remarks>
    /// The enumeration half of ADR 0084, told from outside: the 202 is identical to a known
    /// address's because the request path does not look the address up at all, and the silence
    /// afterwards spends the mailbox helper's whole budget on purpose — a message that has not
    /// arrived yet and one that will never arrive look identical at every earlier instant.
    /// </remarks>
    [Fact]
    public async Task AnAddressNobodyRegistered_IsAnsweredTheSame_AndNothingEverLeaves()
    {
        // Arrange
        var client = Factory.CreateClient();
        var unknown = $"nobody-{Guid.NewGuid():N}@example.com";

        // Act
        var accepted = await ForgotAsync(client, unknown);

        // Assert
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await accepted.Content.ReadAsStringAsync()).Should().BeEmpty(
            "a body would be one more place for the two answers to differ");
        (await Mailbox.NothingEverArrivesAsync(Factory, unknown, LinkSubject))
            .Should().BeTrue("an unknown address costs no email and is told nothing");
    }

    /// <summary>
    /// Asking again, kills the earlier link — only the newest works.
    /// </summary>
    [Fact]
    public async Task AskingAgain_KillsTheEarlierLink_AndOnlyTheNewestWorks()
    {
        // Arrange
        var client = Factory.CreateClient();
        var account = await RegisterAsync(client);

        await ForgotAsync(client, account.Email);
        var first = await EmailedTokenAsync(account.Email);

        // Act
        await ForgotAsync(client, account.Email);
        var second = await EmailedTokenAsync(account.Email, otherThan: first);

        // Assert
        var stale = await ResetAsync(client, account.Email, first, "a-new-password");
        stale.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "issuing a new credential is what invalidates every earlier one, however readable its email remains");

        var fresh = await ResetAsync(client, account.Email, second, "a-new-password");
        fresh.StatusCode.Should().Be(HttpStatusCode.NoContent, "the newest link is the account's one credential");
    }

    /// <summary>
    /// A link past its lifetime, is refused with the same sentence as any other dead link.
    /// </summary>
    /// <remarks>
    /// The row is aged by hand through the host's own container, because the alternative is a
    /// fifteen-minute test: expiry is a timestamp in the schema, which is exactly what makes it
    /// reachable here and provable at all (ADR 0084).
    /// </remarks>
    [Fact]
    public async Task ALinkPastItsLifetime_IsRefused()
    {
        // Arrange
        var client = Factory.CreateClient();
        var account = await RegisterAsync(client);

        await ForgotAsync(client, account.Email);
        var token = await EmailedTokenAsync(account.Email);

        using (var scope = Factory.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<TrainingIdentityDbContext>();

            (await identity.Set<PasswordResetToken>()
                    .ExecuteUpdateAsync(row => row.SetProperty(
                        credential => credential.ExpiresOnUtc,
                        DateTime.UtcNow.AddMinutes(-1))))
                .Should().Be(1, "the credential to age is the one row the request above minted");
        }

        // Act
        var expired = await ResetAsync(client, account.Email, token, "a-new-password");

        // Assert
        expired.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await expired.Content.ReadAsStringAsync()).Should().Contain(RefusedLinkSentence,
            "an expired link answers the one sentence every dead link answers");
    }

    /// <summary>
    /// A wrong token and an unknown address, answer the very same refusal.
    /// </summary>
    /// <remarks>
    /// The oracle this endpoint refuses to be: if the two documents differed by so much as a
    /// word, a caller could sort addresses into accounts and strangers by resetting against
    /// them. Compared as raw bodies so no difference can hide behind a lenient parser.
    /// </remarks>
    [Fact]
    public async Task AWrongTokenAndAnUnknownAddress_AnswerTheVerySameRefusal()
    {
        // Arrange
        var client = Factory.CreateClient();
        var account = await RegisterAsync(client);
        await ForgotAsync(client, account.Email);
        await EmailedTokenAsync(account.Email);

        // Act
        var wrongToken = await ResetAsync(client, account.Email, "not-the-token", "a-new-password");
        var unknownAddress = await ResetAsync(
            client, $"nobody-{Guid.NewGuid():N}@example.com", "not-the-token", "a-new-password");

        // Assert
        wrongToken.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        unknownAddress.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await wrongToken.Content.ReadAsStringAsync()).Should().Be(
            await unknownAddress.Content.ReadAsStringAsync(),
            "one byte of difference between the two refusals is an account-enumeration oracle");
    }

    /// <summary>
    /// A password the policy refuses, is named in full — and the link survives it.
    /// </summary>
    [Fact]
    public async Task APasswordThePolicyRefuses_LeavesTheLinkAlive()
    {
        // Arrange
        var client = Factory.CreateClient();
        var account = await RegisterAsync(client);

        await ForgotAsync(client, account.Email);
        var token = await EmailedTokenAsync(account.Email);

        // Act
        var refused = await ResetAsync(client, account.Email, token, "abc");

        // Assert
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Password",
            "only the credential's owner can reach a password verdict, so it is explained in full");

        var retried = await ResetAsync(client, account.Email, token, "a-new-password");
        retried.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "a policy slip costs the owner a retype, never a trip back to their inbox");
    }

    /// <summary>
    /// The row behind the link, holds a digest and never the token.
    /// </summary>
    [Fact]
    public async Task TheRowBehindTheLink_HoldsADigestAndNeverTheToken()
    {
        // Arrange
        var client = Factory.CreateClient();
        var account = await RegisterAsync(client);

        // Act
        await ForgotAsync(client, account.Email);
        var token = await EmailedTokenAsync(account.Email);

        // Assert
        using var scope = Factory.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<TrainingIdentityDbContext>();

        var row = await identity.Set<PasswordResetToken>().AsNoTracking().SingleAsync();

        row.TokenHash.Should().Equal(SHA256.HashData(Encoding.UTF8.GetBytes(token)),
            "the table holds what a leak cannot redeem: the digest of the emailed token, and nothing else");
        row.ExpiresOnUtc.Should().Be(row.CreatedOnUtc + PasswordResetToken.Lifetime,
            "the fifteen-minute window is stamped at issue, exactly");
    }

    /// <summary>What recovery needs to know about the account a fact just registered.</summary>
    private sealed record Account(string Username, string Email, string Password);

    /// <summary>Registers a fresh account and hands back what recovery needs to know about it.</summary>
    private static async Task<Account> RegisterAsync(HttpClient client)
    {
        var request = AuthHelper.CreateUniqueRegisterRequest();

        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        return new Account(request.Username, request.Email, request.Password);
    }

    private static Task<HttpResponseMessage> ForgotAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/Auth/forgot-password", new ForgotPasswordHttpRequest { Email = email });

    private static Task<HttpResponseMessage> ResetAsync(
        HttpClient client, string email, string token, string password) =>
        client.PostAsJsonAsync("/Auth/reset-password", new ResetPasswordHttpRequest
        {
            Email = email,
            Token = token,
            Password = password,
            ConfirmPassword = password
        });

    /// <summary>
    /// The token inside the freshest reset email for this address — the way the account's owner
    /// gets it, and the only way anyone can: it exists nowhere else.
    /// </summary>
    /// <param name="emailAddress">The recipient whose mailbox is read.</param>
    /// <param name="otherThan">
    /// A token to wait past: Mailpit lists newest first, but the newest email may not have landed
    /// yet when two requests race the mailbox, so the second read polls until the token it sees
    /// is no longer the first one.
    /// </param>
    private async Task<string> EmailedTokenAsync(string emailAddress, string? otherThan = null)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);

        while (true)
        {
            var body = await Mailbox.WaitForEmailAsync(Factory, emailAddress, LinkSubject);
            var token = Uri.UnescapeDataString(TokenInLink.Match(body).Groups[1].Value);

            token.Should().NotBeNullOrWhiteSpace("the reset email's whole point is the link and its token");

            if (otherThan is null || !string.Equals(token, otherThan, StringComparison.Ordinal))
            {
                return token;
            }

            (DateTime.UtcNow < deadline).Should().BeTrue(
                "the second request's email must eventually displace the first's at the top of the mailbox");

            await Task.Delay(100);
        }
    }
}
