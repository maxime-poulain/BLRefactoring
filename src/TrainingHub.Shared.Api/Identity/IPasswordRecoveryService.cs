namespace TrainingHub.Shared.Api.Identity;

/// <summary>
/// The two halves of recovering an account: asking for a reset link, and redeeming one.
/// </summary>
/// <remarks>
/// A shared Identity service in <see cref="TokenService"/>'s mold rather than a use case of
/// either stack: authentication has never lived in the stacks — registration and login are the
/// precedent — so recovery joins them here, written once and published by both hosts through
/// <c>AuthControllerBase</c> (ADR 0084).
/// </remarks>
public interface IPasswordRecoveryService
{
    /// <summary>
    /// Records that a reset was asked for this address, and nothing else.
    /// </summary>
    /// <remarks>
    /// One outbox row and one commit, whether or not the address names an account. The lookup,
    /// the minting and the email all happen later, in the delivery worker — which is what makes
    /// the request path constant work and closes the timing side of account enumeration
    /// (ADR 0084). Whoever asked is answered the same sentence either way.
    /// </remarks>
    /// <param name="emailAddress">The address the visitor asked to recover.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task RequestAsync(string emailAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems a reset link against a new password.
    /// </summary>
    /// <remarks>
    /// Ordered so a good link never burns on a bad password: the link is looked at before the
    /// password validators run, and consumed — atomically — only once the password has passed.
    /// The consumption, the credential write and the changed-password fact commit together.
    /// </remarks>
    /// <param name="emailAddress">The account email, as the reset form collected it.</param>
    /// <param name="token">The reset token, exactly as the emailed link carried it.</param>
    /// <param name="newPassword">The password to set.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<PasswordResetOutcome> ResetAsync(
        string emailAddress,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);
}
