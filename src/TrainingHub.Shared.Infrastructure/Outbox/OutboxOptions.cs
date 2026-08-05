namespace TrainingHub.Shared.Infrastructure.Outbox;

/// <summary>
/// How eagerly the outbox is drained, and when a message is given up on.
/// </summary>
/// <remarks>
/// Bound from the <c>Outbox</c> configuration section; the defaults are deliberately unexciting —
/// latency in seconds is honest for a mechanism whose whole point is surviving restarts, and a
/// host that needs faster delivery turns one knob instead of editing the worker. The values are
/// read once per use through <c>IOptions</c>, so a test can shrink the poll interval to
/// milliseconds without touching production defaults (ADR 0025). Every knob is validated
/// positive at start-up, so a wrong value refuses the host rather than surfacing as the worker's
/// first failure (ADR 0033).
/// </remarks>
public sealed class OutboxOptions
{
    /// <summary>The configuration section the options bind from.</summary>
    public const string SectionName = "Outbox";

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

    /// <summary>
    /// The base of the retry schedule: a failed attempt books its next try this far out, doubling
    /// with every attempt spent — 30 s, 60 s, 120 s… — so an ailing dependency is probed on a
    /// widening cadence instead of hammered until the budget burns (ADR 0033).
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a delivered message is kept before the sweep removes it. Only delivered rows are
    /// ever swept: a poison row is an operator's evidence and stays until they deal with it.
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(14);
}
