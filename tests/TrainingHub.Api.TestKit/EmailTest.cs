using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainers;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// Proves the email path end to end: a fact committed by the API leaves the host as a real SMTP
/// message and arrives addressed, titled and worded as its handler composed it.
/// </summary>
/// <remarks>
/// The messages are read back through the mail server's own HTTP API rather than a substituted
/// port, because delivery is exactly what a substitute would assume: that MailKit connects, that
/// the options point somewhere real, that the sender the deployment configured is the one on the
/// wire. Every recipient below is unique per test — the registration helper mints them — so the
/// mailbox accumulating across a fixture's lifetime cannot make one test read another's message.
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture — one per host, since the wiring under test is
/// each host's own.</typeparam>
public abstract class EmailTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IMailboxSource
{
    /// <summary>
    /// Registering a trainer, delivers the welcome email, through real SMTP.
    /// </summary>
    [Fact]
    public async Task RegisteringATrainer_DeliversTheWelcomeEmail_ThroughRealSmtp()
    {
        var request = AuthHelper.CreateUniqueRegisterRequest();

        var response = await AuthHelper.RegisterAsync(Factory.CreateClient(), request);
        response.EnsureSuccessStatusCode();

        var text = await WaitForEmailAsync(request.Email, "Welcome aboard!");

        text.Should().Contain(
            $"{request.Firstname} {request.Lastname}",
            "the handler addresses the trainer by the name the fact carried");
    }

    /// <summary>
    /// Changing the contact email, warns the address that lost it.
    /// </summary>
    [Fact]
    public async Task ChangingTheContactEmail_WarnsTheAddressThatLostIt()
    {
        // Signed in by hand rather than through the one-call helper, because this test needs to
        // keep the original address: it is the recipient the warning must go to.
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();
        var token = await AuthHelper.LoginAsync(client, request.Username, request.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newAddress = $"changed-{request.Username}@example.com";
        var entityTag = await client.GetETagAsync("/Trainer/me");
        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerHttpRequest
        {
            Firstname = request.Firstname,
            Lastname = request.Lastname,
            ContactEmail = newAddress
        }, entityTag);
        response.EnsureSuccessStatusCode();

        var text = await WaitForEmailAsync(request.Email, "Your contact email address was changed");

        text.Should().Contain(
            newAddress,
            "the warning tells the previous owner where their profile now points");
    }

    /// <summary>
    /// Polls the mailbox until a message addressed to <paramref name="recipient"/> and titled
    /// <paramref name="subject"/> arrives, and answers its plain-text body. Fails with what the
    /// mailbox actually held when the message does not arrive in time.
    /// </summary>
    /// <remarks>
    /// Polling is the honest shape here, as it is for the outbox proofs: the delivery worker is a
    /// real background loop and the SMTP hop is a real network hop. The subject is part of the
    /// match rather than a later assertion because one address legitimately receives more than one
    /// message — the old contact address got a welcome before it got its warning. The whole list is
    /// fetched and filtered client-side rather than through the server's query syntax — the mailbox
    /// belongs to this fixture and stays small, and an address is easier to trust than a query DSL.
    /// </remarks>
    private async Task<string> WaitForEmailAsync(string recipient, string subject)
    {
        using var client = new HttpClient { BaseAddress = Factory.MailboxApiBaseAddress };

        var timeout = TimeSpan.FromSeconds(15);
        var started = DateTime.UtcNow;
        IReadOnlyList<MailpitMessageSummary> lastSeen = [];

        while (DateTime.UtcNow - started < timeout)
        {
            var mailbox = await client.GetFromJsonAsync<MailpitMessageList>("/api/v1/messages");
            lastSeen = mailbox?.Messages ?? [];

            var match = lastSeen.FirstOrDefault(message =>
                message.Subject == subject && message.To.Any(address => address.Address == recipient));

            if (match is not null)
            {
                var detail = await client.GetFromJsonAsync<MailpitMessageDetail>($"/api/v1/message/{match.ID}");
                detail.Should().NotBeNull("the mailbox listed the message a moment ago");
                return detail.Text;
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException(
            $"No message addressed to '{recipient}' with subject '{subject}' arrived within {timeout.TotalSeconds}s. " +
            (lastSeen.Count == 0
                ? "The mailbox is empty."
                : $"The mailbox holds {lastSeen.Count}: " + string.Join("; ", lastSeen.Select(message =>
                    $"To={string.Join(",", message.To.Select(address => address.Address))} Subject='{message.Subject}'")) + "."));
    }

    // The slices of Mailpit's API answers these proofs read. Property names follow the wire
    // (deserialized case-insensitively), and everything not asserted on is left out.

    private sealed record MailpitMessageList(List<MailpitMessageSummary> Messages);

    private sealed record MailpitMessageSummary(string ID, string Subject, List<MailpitAddress> To);

    private sealed record MailpitAddress(string Address);

    private sealed record MailpitMessageDetail(string Text);
}
