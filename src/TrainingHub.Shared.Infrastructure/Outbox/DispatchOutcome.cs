namespace TrainingHub.Shared.Infrastructure.Outbox;

/// <summary>
/// One consumer's failed delivery: who, and what it threw.
/// </summary>
/// <param name="ConsumerName">The ledger identity of the consumer that failed.</param>
/// <param name="Exception">What its reaction threw.</param>
public sealed record ConsumerFailure(string ConsumerName, Exception Exception);

/// <summary>
/// What one dispatch pass settled: which consumers delivered, which failed and why.
/// </summary>
/// <remarks>
/// The per-consumer half of the retry contract (ADR 0034). The processor writes a ledger row per
/// name in <see cref="Delivered"/>, marks the message processed when
/// <see cref="EveryConsumerSettled"/>, and otherwise records the failures on the envelope — so the
/// next attempt re-runs only the consumers still owed.
/// </remarks>
/// <param name="Delivered">The consumers that ran this pass and succeeded — never the skipped.</param>
/// <param name="Failures">The consumers that ran this pass and threw.</param>
public sealed record DispatchOutcome(
    IReadOnlyList<string> Delivered,
    IReadOnlyList<ConsumerFailure> Failures)
{
    /// <summary>Whether nothing is owed anymore: no consumer failed this pass.</summary>
    public bool EveryConsumerSettled => Failures.Count == 0;
}
