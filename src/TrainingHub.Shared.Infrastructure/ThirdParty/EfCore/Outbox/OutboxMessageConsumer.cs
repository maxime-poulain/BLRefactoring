namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;

/// <summary>
/// One consumer's delivery of one outbox message: the row the delivery ledger is made of.
/// </summary>
/// <remarks>
/// The per-consumer half of the retry contract (ADR 0034). The processor writes a row here the
/// moment a consumer's reaction succeeds, in the same save as the message's outcome, and the
/// dispatcher skips every consumer the ledger already names — so a retry re-runs only what is
/// still owed, and a failing neighbor cannot replay a delivered welcome email. Like the envelope
/// itself, this is a persistence shape, not a domain one: no aggregate, no audit, no events, no
/// navigation — the pair of identifiers is the whole fact, which is why the pair is the key.
/// The rows ride the envelope's lifecycle through a cascading foreign key: sweeping or deleting a
/// message takes its ledger with it, in the database engine, no tracker involved.
/// </remarks>
public sealed class OutboxMessageConsumer
{
    /// <summary>
    /// Records that a consumer's reaction to a message succeeded.
    /// </summary>
    /// <param name="messageId">The envelope the delivery belongs to.</param>
    /// <param name="consumerName">The consumer's stable ledger identity.</param>
    /// <param name="deliveredOnUtc">When the reaction completed, in UTC.</param>
    public OutboxMessageConsumer(Guid messageId, string consumerName, DateTime deliveredOnUtc)
    {
        MessageId = messageId;
        ConsumerName = consumerName;
        DeliveredOnUtc = deliveredOnUtc;
    }

    // EF Core materializes rows through this constructor; the public one is for the processor.
    private OutboxMessageConsumer()
    {
        ConsumerName = string.Empty;
    }

    /// <summary>The envelope this delivery belongs to — half of the identity.</summary>
    public Guid MessageId { get; }

    /// <summary>The consumer's stable ledger identity — the other half.</summary>
    public string ConsumerName { get; }

    /// <summary>When the consumer's reaction completed, in UTC.</summary>
    public DateTime DeliveredOnUtc { get; }
}
