using System.Net.Http.Json;
using AwesomeAssertions;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// Reads the fixture's mail server back through its own HTTP API.
/// </summary>
/// <remarks>
/// Shared by every suite that proves an email path, because the alternative is each of them
/// carrying its own copy of the same poll loop and its own idea of how long delivery may take.
/// <para>
/// Polling is the honest shape here, as it is for the outbox proofs: the delivery worker is a real
/// background loop and the SMTP hop is a real network hop. The whole list is fetched and filtered
/// client-side rather than through the server's query syntax — the mailbox belongs to one fixture
/// and stays small, and an address is easier to trust than a query DSL.
/// </para>
/// </remarks>
public static class Mailbox
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Polls until a message addressed to <paramref name="recipient"/> and titled
    /// <paramref name="subject"/> arrives, and answers its plain-text body. Fails with what the
    /// mailbox actually held when the message does not arrive in time.
    /// </summary>
    /// <remarks>
    /// The subject is part of the match rather than a later assertion because one address
    /// legitimately receives more than one message — the old contact address got a welcome before
    /// it got its warning, and a suspended trainer got a welcome before their notice.
    /// </remarks>
    /// <param name="factory">The suite's fixture.</param>
    /// <param name="recipient">The address the message must be addressed to.</param>
    /// <param name="subject">The subject line the message must carry.</param>
    public static async Task<string> WaitForEmailAsync(IMailboxSource factory, string recipient, string subject)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var client = CreateClient(factory);

        var started = DateTime.UtcNow;
        IReadOnlyList<MailpitMessageSummary> lastSeen = [];

        while (DateTime.UtcNow - started < Timeout)
        {
            lastSeen = await ListAsync(client);

            var match = Match(lastSeen, recipient, subject);

            if (match is not null)
            {
                var detail = await client.GetFromJsonAsync<MailpitMessageDetail>($"/api/v1/message/{match.ID}");
                detail.Should().NotBeNull("the mailbox listed the message a moment ago");
                return detail.Text;
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException(
            $"No message addressed to '{recipient}' with subject '{subject}' arrived within {Timeout.TotalSeconds}s. " +
            Describe(lastSeen));
    }

    /// <summary>
    /// Watches for a while and answers whether such a message ever arrived.
    /// </summary>
    /// <remarks>
    /// The shape a negative proof needs, and it costs what a negative proof costs: the full budget,
    /// spent on purpose. A message that has not arrived yet and one that will never arrive look
    /// identical at every earlier instant, so the only honest way to assert the second is to give
    /// delivery the same time the positive proofs get and find nothing.
    /// <para>
    /// It is worth spending only where nothing arriving is the decision itself: the release that
    /// ADR 0052 says earns no fact at all, and the honeypot that ADR 0082 says earns a success and
    /// no message — each indistinguishable, at every earlier instant, from one whose message is
    /// merely slow.
    /// </para>
    /// </remarks>
    /// <param name="factory">The suite's fixture.</param>
    /// <param name="recipient">The address no message must be addressed to.</param>
    /// <param name="subject">The subject line no message must carry.</param>
    public static async Task<bool> NothingEverArrivesAsync(IMailboxSource factory, string recipient, string subject)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var client = CreateClient(factory);

        var started = DateTime.UtcNow;

        while (DateTime.UtcNow - started < Timeout)
        {
            if (Match(await ListAsync(client), recipient, subject) is not null)
            {
                return false;
            }

            await Task.Delay(100);
        }

        return true;
    }

    private static HttpClient CreateClient(IMailboxSource factory) =>
        new() { BaseAddress = factory.MailboxApiBaseAddress };

    private static async Task<IReadOnlyList<MailpitMessageSummary>> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<MailpitMessageList>("/api/v1/messages"))?.Messages ?? [];

    private static MailpitMessageSummary? Match(
        IReadOnlyList<MailpitMessageSummary> mailbox, string recipient, string subject) =>
        mailbox.FirstOrDefault(message =>
            message.Subject == subject && message.To.Any(address => address.Address == recipient));

    private static string Describe(IReadOnlyList<MailpitMessageSummary> mailbox) =>
        mailbox.Count == 0
            ? "The mailbox is empty."
            : $"The mailbox holds {mailbox.Count}: " + string.Join("; ", mailbox.Select(message =>
                $"To={string.Join(",", message.To.Select(address => address.Address))} Subject='{message.Subject}'")) + ".";

    // The slices of Mailpit's API these proofs read. Property names follow the wire (deserialized
    // case-insensitively), and everything not asserted on is left out.

    private sealed record MailpitMessageList(List<MailpitMessageSummary> Messages);

    private sealed record MailpitMessageSummary(string ID, string Subject, List<MailpitAddress> To);

    private sealed record MailpitAddress(string Address);

    private sealed record MailpitMessageDetail(string Text);
}
