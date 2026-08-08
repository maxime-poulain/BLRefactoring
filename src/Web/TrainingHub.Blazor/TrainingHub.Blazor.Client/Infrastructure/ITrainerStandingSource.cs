namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Where the signed-in trainer's standing comes from, and how it is kept current.
/// </summary>
/// <remarks>
/// An API read rather than a claim, and that is not a preference. The token is minted once at
/// sign-in and there is no refresh token, so a suspension decided while the trainer is signed in
/// would leave a claim saying <c>Active</c> until they signed out — which is exactly the window in
/// which the sanction has to hold (ADR 0057).
/// </remarks>
public interface ITrainerStandingSource
{
    /// <summary>Raised when the standing has changed, so a layout can redraw its banner.</summary>
    event Action? Changed;

    /// <summary>The standing, read once and shared by every page that asks.</summary>
    Task<TrainerStanding> GetAsync();

    /// <summary>
    /// Reads it again, discarding what was cached.
    /// </summary>
    /// <remarks>
    /// Called when a write comes back <c>403</c>: the refusal carries no body by design, so the
    /// answer to "why" is one read away on a surface the sanction deliberately leaves open.
    /// </remarks>
    Task<TrainerStanding> RefreshAsync();
}
