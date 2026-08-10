namespace TrainingHub.Shared.Application.Queries;

/// <summary>
/// Answers whether a trainer is under suspension, without loading the trainer.
/// </summary>
/// <remarks>
/// The authorization policy that guards every write of the trainer surface needs one bit: whether
/// the caller's standing is <c>Suspended</c> (ADR 0053). It reads one column and materializes
/// nothing, on the authorization path, which every guarded write goes through — the same reasoning
/// that made <see cref="ITrainingOwnerQuery"/> a query rather than a repository call.
/// <para>
/// The domain declares a port of its own with the same question,
/// <c>ITrainerStanding.IsSuspendedAsync</c>, and the duplication is deliberate rather than
/// accidental. That one exists so <c>Training.CreateAsync</c> and <c>PublishAsync</c> can settle a
/// business rule; this one exists so the boundary can refuse a request before any use case runs.
/// They survive each other: ADR 0053 keeps the domain's rules as defense in depth precisely because
/// the boundary is not the only door, and a boundary borrowing a domain port would make the API
/// depend on a contract that exists for a different reason — the direction the dependency rule
/// does not allow.
/// </para>
/// </remarks>
public interface ITrainerStandingQuery
{
    /// <summary>
    /// Whether the trainer is currently suspended.
    /// </summary>
    /// <remarks>
    /// A trainer no row answers to is not suspended. The policy's job is to refuse a sanctioned
    /// caller, not to prove that a caller exists: an identifier naming nobody is a question for the
    /// action, which answers <c>404</c>, and turning it into a <c>403</c> here would tell the caller
    /// something false about their permissions.
    /// </remarks>
    Task<bool> IsSuspendedAsync(Guid trainerId, CancellationToken cancellationToken = default);
}
