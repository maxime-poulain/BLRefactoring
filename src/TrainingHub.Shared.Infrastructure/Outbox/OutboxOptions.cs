namespace TrainingHub.Shared.Infrastructure.Outbox;

/// <summary>
/// How eagerly the outbox is drained, and when a message is given up on.
/// </summary>
/// <remarks>
/// Bound from the <c>Outbox</c> configuration section; the defaults are deliberately unexciting —
/// latency in seconds is honest for a mechanism whose whole point is surviving restarts, and a
/// host that needs faster delivery turns one knob instead of editing the worker. The values are
/// read once per use through <c>IOptions</c>, so a test can shrink the poll interval to
/// milliseconds without touching production defaults (ADR 0025).
/// </remarks>
public sealed class OutboxOptions
{
    /// <summary>How long the worker sleeps between polls once the table is drained.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>How many messages one poll claims at most.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// How many failed deliveries a message is allowed before it is poison: still stored, never
    /// retried, its last error kept beside it for the operator.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// How long a claim lasts. A worker that dies mid-delivery simply lets its lease lapse, and
    /// the message becomes claimable again — the crash-safety half of at-least-once.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
}
