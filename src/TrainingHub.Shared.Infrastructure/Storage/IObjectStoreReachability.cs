namespace TrainingHub.Shared.Infrastructure.Storage;

/// <summary>
/// Answers whether the object store is reachable, for the readiness probe alone.
/// </summary>
/// <remarks>
/// A port with one consumer: the health check in <c>Shared.Api</c>. It exists because the AWS SDK
/// may only be named by the infrastructure (ADR 0021's confinement rule), and it is deliberately
/// not a fourth method on <see cref="TrainingHub.Shared.Storage.IObjectStore"/> — that interface
/// is the application's storage vocabulary, pinned to its three verbs, and "are you there?" is an
/// operational question, not a storage one (ADR 0037).
/// </remarks>
public interface IObjectStoreReachability
{
    /// <summary>
    /// Performs one real, signed read against the store, throwing when it cannot be completed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>A task that completes when the store answered, and faults when it did not.</returns>
    Task ProbeAsync(CancellationToken cancellationToken = default);
}
