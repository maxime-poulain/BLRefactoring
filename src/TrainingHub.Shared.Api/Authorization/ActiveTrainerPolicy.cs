namespace TrainingHub.Shared.Api.Authorization;

/// <summary>
/// The authorization policy guarding every write of the trainer surface: the caller must be a
/// trainer whose standing is <c>Active</c>.
/// </summary>
/// <remarks>
/// Named at each write rather than carried by <c>ApiControllerBase</c>, because it is the one
/// distinction a suspended trainer is entitled to: they keep every read and lose every write
/// (ADR 0053). A policy on the base would take their profile and their catalogue away from them
/// too, which is the alternative that record rejected by name — "hiding them from their owner
/// serves nobody".
/// <para>
/// It combines with <see cref="TrainerPolicy"/> rather than replacing it, which is what makes
/// naming it per action safe: a write ends up behind "is somebody's trainer" and "is not
/// suspended", and a read behind the first alone.
/// </para>
/// <para>
/// The browser is expected never to reach this refusal — the trainer's own screens disable what
/// they may not do (ADR 0057). That is courtesy, not security: this policy is what actually
/// refuses, and it answers a request that arrives by any other route as well.
/// </para>
/// </remarks>
public static class ActiveTrainerPolicy
{
    /// <summary>
    /// Name under which the policy is registered and referenced from
    /// <c>[Authorize(Policy = ...)]</c>.
    /// </summary>
    public const string Name = "ActiveTrainer";
}
