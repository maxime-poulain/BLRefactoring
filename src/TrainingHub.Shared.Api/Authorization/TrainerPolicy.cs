namespace TrainingHub.Shared.Api.Authorization;

/// <summary>
/// The authorization policy guarding the trainer surface: the caller must be somebody's trainer.
/// </summary>
/// <remarks>
/// Carried by <c>ApiControllerBase</c>, so it holds for every action of both hosts rather than for
/// a chosen few. It exists because an administrator is an account and not a trainer (ADR 0051):
/// their token carries no <see cref="Identity.TrainerClaims.TrainerId"/>, and every one of the
/// eighteen places that read it would otherwise raise — answering <c>500</c> to a caller whose only
/// mistake was calling an endpoint that is not theirs.
/// <para>
/// Refusing once at the boundary is what keeps those eleven call sites free of the question.
/// The alternative — a member on the current-user accessor saying "this caller is nobody's
/// trainer" — would be answered by nothing, since no use case behind this policy can meet the
/// case, and an accessor nobody consults is not a defense.
/// </para>
/// </remarks>
public static class TrainerPolicy
{
    /// <summary>
    /// Name under which the policy is registered and referenced from
    /// <c>[Authorize(Policy = ...)]</c>.
    /// </summary>
    public const string Name = "Trainer";
}
