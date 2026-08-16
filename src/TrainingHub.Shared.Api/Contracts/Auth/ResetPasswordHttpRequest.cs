using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Auth;

/// <summary>
/// The body of <c>POST /Auth/reset-password</c>: the emailed credential, the account it claims,
/// and the password it buys.
/// </summary>
/// <remarks>
/// The email travels in the body rather than in the emailed link, so the link carries nothing
/// but the token — no address in a browser's history or a proxy's log (ADR 0084). No length
/// bounds on the token or the password: like <see cref="LoginHttpRequest"/>, a shape rule on a
/// credential would answer a malformed one differently from a wrong one.
/// </remarks>
public sealed class ResetPasswordHttpRequest
{
    /// <summary>
    /// The account email the reset was requested for — typed again on the form, never carried by
    /// the link.
    /// </summary>
    [Required]
    public required string Email { get; init; }

    /// <summary>
    /// The reset token, exactly as the emailed link carried it.
    /// </summary>
    [Required]
    public required string Token { get; init; }

    /// <summary>
    /// The new password.
    /// </summary>
    [Required]
    public required string Password { get; init; }

    /// <summary>
    /// The new password, typed again.
    /// </summary>
    [Required]
    public required string ConfirmPassword { get; init; }
}
