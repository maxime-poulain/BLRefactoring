namespace TrainingHub.Shared.Api.Identity;

/// <summary>
/// The claims this API mints itself, as opposed to the ones
/// <see cref="System.Security.Claims.ClaimTypes"/> already names.
/// </summary>
/// <remarks>
/// One name, written once. The token writes it, the accessor reads it back, the ownership handler
/// compares against it and the trainer policy demands it — four places, and a typo in any one of
/// them is silent: a claim that is never found looks exactly like a caller who is not a trainer.
/// </remarks>
public static class TrainerClaims
{
    /// <summary>
    /// The trainer the signed-in account is, absent from the token of an account that is nobody's
    /// trainer — an administrator, for instance.
    /// </summary>
    public const string TrainerId = "trainer_id";
}
