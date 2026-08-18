using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Auth;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Contracts.Trainings;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The verification flow of ADR 0090, proven whole: the invitation travels by real SMTP at
/// registration, the credential lives hashed in the real database, the click opens the one door
/// it guards, and every property the record claims — latest-only, single-use, one sentence for
/// every dead link, one window per account — is a fact here rather than a sentence there.
/// </summary>
/// <remarks>
/// The email is the API of this flow, exactly as it is for the recovery's: the token exists
/// nowhere else, and reading it out of Mailpit is what the account's owner does. Every account
/// in this suite is registered raw — never through
/// <see cref="AuthHelper.RegisterAndGetAuthenticatedClientAsync{TFactory}"/>, whose scope-flip
/// exists so the <em>other</em> suites need not click; the honest click lives here and only
/// here.
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract partial class EmailVerificationTest<TFactory>(TFactory factory)
    : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IMailboxSource, IServiceScopeSource
{
    private const string LinkSubject = "Verify your TrainingHub email address";

    private const string RefusedLinkSentence = "This verification link is invalid or has already been used.";

    [GeneratedRegex(@"token=(\S+)")]
    private static partial Regex TokenInLink { get; }

    /// <summary>
    /// Registering, delivers a link that opens the catalog door — and the link dies on use.
    /// </summary>
    [Fact]
    public async Task Registering_DeliversALinkThatOpensTheCatalogDoor_ThroughRealSmtp()
    {
        // Arrange
        var client = Factory.CreateClient();
        var account = await RegisterAsync(client);

        // Act — the door is closed before the click, and the refusal is the boundary's: a bare
        // 403, because the policy answers before any use case runs (ADR 0090).
        await SignInAsync(client, account);
        var refused = await client.PostAsJsonAsync("/Training", TrainingRequests.Valid("Before the click"));

        var token = await EmailedTokenAsync(account.Email);
        var verified = await VerifyAsync(client, token);
        var admitted = await client.PostAsJsonAsync("/Training", TrainingRequests.Valid("After the click"));

        // Assert
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().BeEmpty(
            "a 403 explains nothing — the browser's banner already says what to do (ADR 0090)");

        verified.StatusCode.Should().Be(HttpStatusCode.NoContent);
        admitted.StatusCode.Should().Be(
            HttpStatusCode.Created, "the click is the whole difference between the two attempts");

        // And the click is not a session artifact: the very token the door refused now passes,
        // because the policy reads the database rather than the claims.
        var replay = await VerifyAsync(client, token);
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest, "consumption is deletion");
        (await Sentence(replay)).Should().Contain(RefusedLinkSentence);
    }

    /// <summary>
    /// The domain refuses the unverified giver too, in its own words.
    /// </summary>
    /// <remarks>
    /// The defense in depth behind the courtesy 403 above cannot be shown on <c>POST /Training</c>
    /// — the boundary answers first — so it is shown where the boundary cannot see the person
    /// being judged: the transfer's recipient is in the body, not in the token, and the refusal
    /// that arrives is the domain's own code (ADR 0090).
    /// </remarks>
    [Fact]
    public async Task TransferringToAnUnverifiedRecipient_IsRefusedByTheDomain()
    {
        // Arrange — a verified giver with a training, and a recipient who never clicked.
        var giver = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var created = await giver.PostAsJsonAsync("/Training", TrainingRequests.Valid("A training to hand over"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var trainingId = await created.Content.ReadFromJsonAsync<Guid>();

        var recipientClient = Factory.CreateClient();
        var recipientAccount = await RegisterAsync(recipientClient);
        await SignInAsync(recipientClient, recipientAccount);
        var recipientId = (await recipientClient.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!.Id;

        // Act
        var refused = await giver.PostAsJsonAsync(
            $"/Training/{trainingId}/transfer",
            new TransferTrainingHttpRequest { RecipientTrainerId = recipientId });

        // Assert
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("domainErrors")
            .EnumerateArray()
            .Select(error => error.GetProperty("errorCode").GetString())
            .Should().Contain(TrainingErrorCodes.RecipientUnverified.Value);
    }

    /// <summary>
    /// Asking again, kills the earlier link — only the most recent email works.
    /// </summary>
    [Fact]
    public async Task AskingAgain_KillsTheEarlierLink()
    {
        // Arrange
        var client = Factory.CreateClient();
        var account = await RegisterAsync(client);
        var firstToken = await EmailedTokenAsync(account.Email);

        await SignInAsync(client, account);

        // Act
        var resent = await client.PostAsync("/Auth/resend-verification", content: null);
        var secondToken = await EmailedTokenAsync(account.Email, otherThan: firstToken);

        var deadFirst = await VerifyAsync(client, firstToken);
        var liveSecond = await VerifyAsync(client, secondToken);

        // Assert
        resent.StatusCode.Should().Be(HttpStatusCode.Accepted);
        deadFirst.StatusCode.Should().Be(
            HttpStatusCode.BadRequest, "the account is the primary key — asking again replaces");
        (await Sentence(deadFirst)).Should().Contain(RefusedLinkSentence);
        liveSecond.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// The sixth resend in a window, is refused at the api's own door.
    /// </summary>
    /// <remarks>
    /// Per account rather than per address, because on this route the identity is real — signed
    /// into the bearer token. One permit is one email <em>and one revocation of the pending
    /// link</em>, which is why the window exists at all (ADR 0090).
    /// </remarks>
    [Fact]
    public async Task TheSixthResendInAWindow_IsRefused()
    {
        // Arrange
        var client = Factory.CreateClient();
        var account = await RegisterAsync(client);
        await SignInAsync(client, account);

        // Act — five permitted, however the earlier ones ended.
        for (var sent = 0; sent < 5; sent++)
        {
            var admitted = await client.PostAsync("/Auth/resend-verification", content: null);

            admitted.StatusCode.Should().Be(
                HttpStatusCode.Accepted, "five asks in fifteen minutes is a person hunting their inbox");
        }

        var refused = await client.PostAsync("/Auth/resend-verification", content: null);

        // Assert
        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await refused.Content.ReadAsStringAsync()).Should().BeEmpty(
            "the shape of the window is not something the endpoint owes its caller");
    }

    /// <summary>
    /// An already verified account's resend, answers the same 202 and mails nothing.
    /// </summary>
    /// <remarks>
    /// The silent absorption at the mint (ADR 0090): the consumer finds the flag set and sends
    /// nothing, so the endpoint's answer cannot say which accounts are done — and a retry of a
    /// delivery that lost its race stays idempotent by the same null.
    /// </remarks>
    [Fact]
    public async Task AnAlreadyVerifiedAccountsResend_AnswersTheSame202_AndMailsNothing()
    {
        // Arrange
        var client = Factory.CreateClient();
        var account = await RegisterAsync(client);
        var token = await EmailedTokenAsync(account.Email);
        await SignInAsync(client, account);
        (await VerifyAsync(client, token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var resent = await client.PostAsync("/Auth/resend-verification", content: null);

        // Assert — the 202 is constant, and the mailbox never gains a second invitation: the
        // negative proof spends the full budget waiting for a token that must never exist.
        resent.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var secondToken = await TryEmailedTokenAsync(account.Email, otherThan: token);
        secondToken.Should().BeNull("a proven address has nothing left to prove, so nothing is minted");
    }

    /// <summary>
    /// A fabricated token, answers the one sentence every dead link gets.
    /// </summary>
    [Fact]
    public async Task AFabricatedToken_AnswersTheOneSentenceEveryDeadLinkGets()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var refused = await VerifyAsync(client, "not-a-token-anyone-was-sent");

        // Assert
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Sentence(refused)).Should().Contain(
            RefusedLinkSentence,
            "a guess, a superseded link and a spent one must be indistinguishable (ADR 0090)");
    }

    /// <summary>
    /// A french registration, is invited in french.
    /// </summary>
    /// <remarks>
    /// The culture crosses the outbox on the fact and the words are composed at delivery, in the
    /// infrastructure's presenter — this is the one integration fact that proves the whole chain
    /// speaks the requester's language (ADR 0088, ADR 0090).
    /// </remarks>
    [Fact]
    public async Task AFrenchRegistration_IsInvitedInFrench()
    {
        // Arrange
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("fr");

        // Act
        var account = await RegisterAsync(client);

        // Assert — the subject is the language's fingerprint, stable per language by design.
        var body = await Mailbox.WaitForEmailAsync(
            Factory, account.Email, "Vérifiez votre adresse e-mail TrainingHub");

        body.Should().Contain("token=", "the invitation carries the link whatever the language");
    }

    /// <summary>The username, address and password of a raw registration — nobody clicked yet.</summary>
    private sealed record Account(string Username, string Email, string Password);

    private static async Task<Account> RegisterAsync(HttpClient client)
    {
        var request = AuthHelper.CreateUniqueRegisterRequest();

        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        return new Account(request.Username, request.Email, request.Password);
    }

    private static async Task SignInAsync(HttpClient client, Account account)
    {
        var token = await AuthHelper.LoginAsync(client, account.Username, account.Password);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private static Task<HttpResponseMessage> VerifyAsync(HttpClient client, string token) =>
        client.PostAsJsonAsync("/Auth/verify-email", new VerifyEmailHttpRequest { Token = token });

    /// <summary>The one sentence of a refusal, read off the problem document's Token field.</summary>
    private static async Task<string> Sentence(HttpResponseMessage refusal)
    {
        var body = JsonDocument.Parse(await refusal.Content.ReadAsStringAsync()).RootElement;

        return body.GetProperty("errors").GetProperty("Token")[0].GetString()!;
    }

    /// <summary>
    /// The token inside the freshest verification email for this address — the way the account's
    /// owner gets it, and the only way anyone can: it exists nowhere else.
    /// </summary>
    /// <param name="emailAddress">The recipient whose mailbox is read.</param>
    /// <param name="otherThan">
    /// A token to wait past: Mailpit lists newest first, but the newest email may not have landed
    /// yet when a resend races the mailbox, so the second read polls until the token it sees is
    /// no longer the first one.
    /// </param>
    private async Task<string> EmailedTokenAsync(string emailAddress, string? otherThan = null) =>
        await TryEmailedTokenAsync(emailAddress, otherThan)
        ?? throw new InvalidOperationException(
            $"No verification email for '{emailAddress}' carried a fresh token in time.");

    /// <summary>The same read, answering nothing instead of failing when no fresh token lands.</summary>
    private async Task<string?> TryEmailedTokenAsync(string emailAddress, string? otherThan = null)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            var body = await Mailbox.WaitForEmailAsync(Factory, emailAddress, LinkSubject);
            var token = Uri.UnescapeDataString(TokenInLink.Match(body).Groups[1].Value);

            token.Should().NotBeNullOrWhiteSpace("the invitation's whole point is the link and its token");

            if (otherThan is null || !string.Equals(token, otherThan, StringComparison.Ordinal))
            {
                return token;
            }

            await Task.Delay(100);
        }

        return null;
    }
}
