namespace TrainingHub.Shared.Application.Dtos.Outbox;

/// <summary>
/// One message the outbox gave up on, as an operator reads it.
/// </summary>
/// <remarks>
/// A message becomes poison when its retry budget is spent and it is still undelivered. It stays in
/// the table on purpose — the sweep removes delivered rows only, because deleting a failure would be
/// the mechanism destroying its own evidence (ADR 0033) — and until now that evidence was reachable
/// only through a log search or a query run by hand (ADR 0061).
/// <para>
/// The payload is deliberately absent. An integration event carries what the fact was about, which
/// for several of them includes an address a person is reached at; deciding whether to retry needs
/// to know <em>what</em> failed, <em>when</em>, and <em>why</em>, and none of those is the content.
/// </para>
/// <para>
/// <see cref="DeliveredConsumers"/> is the half that makes a retry legible rather than alarming: the
/// ledger's rows survive a requeue, so the consumers named here are exactly the ones a retry will
/// <em>not</em> run again (ADR 0034).
/// </para>
/// </remarks>
public sealed class PoisonedMessageDto
{
    /// <summary>The envelope's identifier — what a requeue names.</summary>
    public required Guid Id { get; init; }

    /// <summary>The event's stable wire name, as the registry publishes it.</summary>
    public required string Name { get; init; }

    /// <summary>The version of that wire name.</summary>
    public required int Version { get; init; }

    /// <summary>When the fact was recorded, in UTC.</summary>
    public required DateTime OccurredOnUtc { get; init; }

    /// <summary>How many deliveries failed — the budget, spent.</summary>
    public required int Attempts { get; init; }

    /// <summary>The last delivery failure, as the processor recorded it.</summary>
    public string? Error { get; init; }

    /// <summary>When an operator last requeued this message, if one ever did.</summary>
    public DateTime? RequeuedOnUtc { get; init; }

    /// <summary>The consumers the ledger already settled, which a retry will not run again.</summary>
    public required IReadOnlyList<string> DeliveredConsumers { get; init; }
}
