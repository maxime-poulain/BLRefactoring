namespace TrainingHub.Blazor.Client.Authorization;

/// <summary>
/// The authorization policies this browser evaluates against the session cookie's identity.
/// </summary>
/// <remarks>
/// One policy, and it is a mirror rather than an invention: the API refuses the trainer surface to
/// a caller whose token carries no <see cref="SessionClaims.TrainerId"/>, and the browser asks the
/// same question of the same claim, under the same name, so the two can never drift into two
/// notions of what a trainer is (ADR 0051, ADR 0078).
/// <para>
/// Asking it here changes nothing about who may call what. The API is still the only thing that
/// decides, and it still answers <c>403</c> to an administrator whatever this browser renders
/// (ADR 0057). What the mirror buys is that the doors offered are the doors that open, and that a
/// call certain to be refused is never made.
/// </para>
/// </remarks>
public static class SessionPolicies
{
    /// <summary>
    /// Name of the policy demanding that the caller be somebody's trainer.
    /// </summary>
    /// <remarks>
    /// Character for character the API's own <c>TrainerPolicy.Name</c>, which is what the rule
    /// defending ADR 0078 checks.
    /// </remarks>
    public const string Trainer = "Trainer";
}
