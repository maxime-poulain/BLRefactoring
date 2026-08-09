namespace TrainingHub.Shared.Api.Contracts.Outbox;

/// <summary>
/// A row of <c>GET /Administration/Outbox/poison</c>: an integration event the system committed and
/// then failed to tell anybody about.
/// </summary>
/// <remarks>
/// The only published contract in this API that describes the platform rather than the domain, and
/// it is deliberately not shaped like the others: no aggregate is behind it, there is nothing to
/// edit, and the single action it enables is a retry (ADR 0061).
/// <para>
/// No payload. Several integration events carry an address a person is reached at, and an operator
/// deciding whether to press retry needs what failed, when, and why — none of which is the content
/// of the message. <see cref="Error"/> is the exception the last attempt reported, stack trace
/// included, which is why this listing is behind the administrator and nowhere else.
/// </para>
/// </remarks>
public sealed class PoisonedMessageHttpResponse
{
    /// <summary>The message identifier, which the requeue action takes.</summary>
    public required Guid Id { get; init; }

    /// <summary>The event's stable wire name — never a CLR type name (ADR 0024).</summary>
    public required string Name { get; init; }

    /// <summary>The version of that wire name.</summary>
    public required int Version { get; init; }

    /// <summary>When the fact was recorded, in UTC.</summary>
    public required DateTime OccurredOnUtc { get; init; }

    /// <summary>How many deliveries failed before the budget ran out.</summary>
    public required int Attempts { get; init; }

    /// <summary>What the last attempt reported, or <see langword="null"/> if nothing did.</summary>
    public string? Error { get; init; }

    /// <summary>When an operator last requeued this message, if one ever did.</summary>
    public DateTime? RequeuedOnUtc { get; init; }

    /// <summary>
    /// The consumers that already succeeded, and which a retry will therefore not run again.
    /// </summary>
    /// <remarks>
    /// The delivery ledger's rows outlive a requeue on purpose, so this is the list that makes
    /// pressing retry twice a safe thing to do rather than a hopeful one (ADR 0034).
    /// </remarks>
    public required IReadOnlyList<string> DeliveredConsumers { get; init; }
}
