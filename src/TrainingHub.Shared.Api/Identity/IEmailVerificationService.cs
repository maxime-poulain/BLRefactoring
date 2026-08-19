namespace TrainingHub.Shared.Api.Identity;

/// <summary>
/// The two halves of proving an account's email address: asking for a verification link, and
/// redeeming one.
/// </summary>
/// <remarks>
/// A shared Identity service in <see cref="IPasswordRecoveryService"/>'s mold rather than a use
/// case of either stack, for the same reason: the address being proven belongs to the account,
/// authentication has never lived in the stacks, and both hosts publish the same endpoints from
/// one implementation through <c>AuthControllerBase</c> (ADR 0090).
/// </remarks>
public interface IEmailVerificationService
{
    /// <summary>
    /// Records that this account asked to prove its address, and nothing else.
    /// </summary>
    /// <remarks>
    /// One outbox row and one commit — the recovery request's shape (ADR 0084). The minting and
    /// the email happen later, in the delivery worker, and an account that is gone or already
    /// verified by then is absorbed there in silence, which is what lets this path answer the
    /// same way every time (ADR 0090).
    /// </remarks>
    /// <param name="userId">The account asking to prove its address.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task RequestAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems an emailed verification link, proving the address it was sent to.
    /// </summary>
    /// <remarks>
    /// The consumption and the proof commit together: the credential is atomically spent, then
    /// the flag is written through the framework's own front door — a confirmation token minted
    /// and burned inside this very call, so the security-stamp bookkeeping stays Identity's. A
    /// refusal on that second step rolls the consumption back, and the visitor may simply try
    /// their link again — a good link is never burned on a failure (ADR 0084's discipline,
    /// ADR 0090).
    /// </remarks>
    /// <param name="token">The raw token, exactly as the emailed link carried it.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> when the address is proven; <see langword="false"/> for every way a
    /// link can be dead — unknown, superseded, already spent, or naming an account that is gone —
    /// so no caller learns which of them it was.
    /// </returns>
    Task<bool> VerifyAsync(string token, CancellationToken cancellationToken = default);
}
