namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Where the signed-in trainer stands, as the front end needs to know it.
/// </summary>
/// <remarks>
/// Three fields rather than the whole profile: the pages that read this care whether they may
/// write and what to say if they may not (ADR 0053, ADR 0057, ADR 0090).
/// </remarks>
/// <param name="IsSuspended">Whether a suspension is in force.</param>
/// <param name="Reason">The administrator's words, or <see langword="null"/> when none applies.</param>
/// <param name="IsEmailVerified">
/// Whether the account has proven its email address. Defaults to <see langword="true"/> for the
/// suspension flag's reason, mirrored: a banner that appeared because a read failed would nag
/// somebody who already clicked their link, while a create attempted in the other direction
/// costs a <c>403</c> the API answers anyway (ADR 0090).
/// </param>
public sealed record TrainerStanding(bool IsSuspended, string? Reason, bool IsEmailVerified = true)
{
    /// <summary>
    /// What every caller sees until told otherwise.
    /// </summary>
    /// <remarks>
    /// Erring toward "active" is deliberate. A banner that appeared because a read failed would
    /// accuse somebody of being sanctioned when they are not; a write attempted in the other
    /// direction costs a <c>403</c> the API answers anyway.
    /// </remarks>
    public static readonly TrainerStanding Active = new(IsSuspended: false, Reason: null);
}
