namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;

/// <summary>
/// The envelope an integration event is stored in: the row the transactional outbox is made of.
/// </summary>
/// <remarks>
/// A persistence shape, not a domain one — deliberately neither an aggregate nor auditable nor a
/// holder of domain events, so neither interceptor looks at it twice. The identity of the message
/// is the envelope, not the payload: <see cref="Id"/> is minted once at publish time and doubles as
/// the deduplication key consumers use to make at-least-once delivery safe, and
/// <see cref="Name"/>/<see cref="Version"/> say what the payload deserializes into without trusting
/// a CLR type name that a refactoring could change.
/// <para>
/// <see cref="ProcessedOnUtc"/>, <see cref="Attempts"/> and <see cref="Error"/> are written by
/// nobody in this codebase yet: they are the delivery worker's contract, declared with the schema
/// so the table is one migration rather than two (ADR 0024). The worker marks a delivered message
/// by stamping <see cref="ProcessedOnUtc"/>, counts each try in <see cref="Attempts"/>, and parks
/// the last failure in <see cref="Error"/> — a poison message is one whose attempts exhausted the
/// policy, still unprocessed, with the reason sitting beside it.
/// </para>
/// </remarks>
public sealed class OutboxMessage
{
    /// <summary>
    /// Stores an integration event under its envelope.
    /// </summary>
    /// <param name="id">The message identifier — also the consumer-side deduplication key.</param>
    /// <param name="name">The stable wire name of the event, from the registry.</param>
    /// <param name="version">The version of that wire name.</param>
    /// <param name="payload">The event, serialized as JSON.</param>
    /// <param name="occurredOnUtc">When the fact was recorded, in UTC.</param>
    public OutboxMessage(Guid id, string name, int version, string payload, DateTime occurredOnUtc)
    {
        Id = id;
        Name = name;
        Version = version;
        Payload = payload;
        OccurredOnUtc = occurredOnUtc;
    }

    // EF Core materializes rows through this constructor; the public one is for the publisher.
    private OutboxMessage()
    {
        Name = string.Empty;
        Payload = string.Empty;
    }

    /// <summary>The message identifier — minted once at publish time, the deduplication key.</summary>
    public Guid Id { get; }

    /// <summary>The stable wire name of the event, as registered — never a CLR type name.</summary>
    public string Name { get; }

    /// <summary>The version of the wire name, so a payload outlives its first schema.</summary>
    public int Version { get; }

    /// <summary>The event, serialized as JSON.</summary>
    public string Payload { get; }

    /// <summary>When the fact was recorded, in UTC — the order the worker delivers in.</summary>
    public DateTime OccurredOnUtc { get; }

    /// <summary>When the worker delivered the message; <see langword="null"/> while it is owed.</summary>
    public DateTime? ProcessedOnUtc { get; }

    /// <summary>How many deliveries have been tried — the retry policy's counter.</summary>
    public int Attempts { get; }

    /// <summary>The last delivery failure, kept beside the message it poisoned.</summary>
    public string? Error { get; }
}
