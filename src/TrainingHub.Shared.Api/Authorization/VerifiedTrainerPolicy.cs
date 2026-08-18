namespace TrainingHub.Shared.Api.Authorization;

/// <summary>
/// The authorization policy guarding the one door verification opens: the caller must be a
/// trainer whose account has proven its email address.
/// </summary>
/// <remarks>
/// On <c>CreateTrainingAsync</c> alone, and the narrowness is the decision (ADR 0090): an
/// unverified trainer signs in, manages their profile, erases their account — everything but
/// growing the public catalog, and creation is the only door into it an unverified trainer can
/// reach, because verification is never revoked and so an unverified trainer can never already
/// own a training to publish or edit. The transfer recipient is precisely the case this policy
/// cannot see — a policy reads the caller, the recipient is in the body — which is why the
/// domain's own check exists and this one is the courtesy in front of it, exactly the division
/// of labor ADR 0053 established for the suspension.
/// <para>
/// It combines with <see cref="TrainerPolicy"/> and <see cref="ActiveTrainerPolicy"/> rather
/// than replacing either: the create door ends up behind "is somebody's trainer", "is not
/// suspended" and "has proven their address", each refusing its own thing.
/// </para>
/// <para>
/// The browser is expected never to reach this refusal — the create doors are disabled while
/// the banner asks for the click (ADR 0057). That is courtesy, not security: this policy is
/// what actually refuses, whatever route the request arrives by.
/// </para>
/// </remarks>
public static class VerifiedTrainerPolicy
{
    /// <summary>
    /// Name under which the policy is registered and referenced from
    /// <c>[Authorize(Policy = ...)]</c>.
    /// </summary>
    public const string Name = "VerifiedTrainer";
}
