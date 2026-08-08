namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Where the signed-in trainer stands, as the front end needs to know it.
/// </summary>
/// <remarks>
/// Two fields rather than the whole profile: the pages that read this care whether they may write
/// and what to say if they may not (ADR 0053, ADR 0057).
/// </remarks>
/// <param name="IsSuspended">Whether a suspension is in force.</param>
/// <param name="Reason">The administrator's words, or <see langword="null"/> when none applies.</param>
public sealed record TrainerStanding(bool IsSuspended, string? Reason)
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

    /// <summary>
    /// The one sentence every disabled control shows, written once because it is the same sentence.
    /// </summary>
    public const string WhyDisabled = "Suspended accounts cannot make changes.";
}
