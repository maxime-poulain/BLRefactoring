namespace BLRefactoring.Shared.Infrastructure.Outbox;

/// <summary>
/// An integration event waiting to be published, stored in the same transaction as the business
/// change that produced it.
/// </summary>
/// <remarks>
/// Not an aggregate and not part of the domain: this is a piece of delivery machinery that
/// happens to live in the business database, because sharing the transaction is the whole point.
/// It is a plain entity of the infrastructure layer, with public setters, because the processor
/// updates it directly rather than through behaviour.
/// </remarks>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Assembly-qualified name of the integration event, kept in its own column rather than
    /// inside the payload. A <c>$type</c> discriminator embedded in the JSON would make the
    /// stored document depend on the serialiser's polymorphism settings; a column can be
    /// queried, indexed and migrated on its own.
    /// </summary>
    public required string Type { get; init; }

    public required string Payload { get; init; }

    /// <summary>
    /// When the fact happened. Ordering the queue by this rather than by insertion keeps
    /// publication in the order the domain produced the events.
    /// </summary>
    public required DateTimeOffset OccurredOn { get; init; }

    /// <summary>
    /// When publication succeeded, or <see langword="null"/> while pending. Processed rows are
    /// kept rather than deleted: the table doubles as an audit trail and makes replay possible.
    /// </summary>
    public DateTimeOffset? ProcessedOn { get; set; }

    /// <summary>
    /// How many times publication has been attempted, successfully or not. Lets a poison
    /// message be spotted instead of being retried forever in silence.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// The last failure, kept so a stuck message can be diagnosed from the table alone.
    /// </summary>
    public string? Error { get; set; }
}
