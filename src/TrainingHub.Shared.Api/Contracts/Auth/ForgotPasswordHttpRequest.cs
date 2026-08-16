using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Auth;

/// <summary>
/// The body of <c>POST /Auth/forgot-password</c>: the address whose account wants its password
/// reset.
/// </summary>
/// <remarks>
/// Presence is the only thing the boundary checks. No <c>[EmailAddress]</c>, for the reason
/// <see cref="RegisterHttpRequest"/> gives (ADR 0043) — and here with a second edge: this request
/// is answered <c>202</c> whether or not the address names an account (ADR 0084), and a format
/// refusal would be one more way for the answer to vary with the input.
/// </remarks>
public sealed class ForgotPasswordHttpRequest
{
    /// <summary>
    /// The account email of the person who forgot their password.
    /// </summary>
    [Required]
    public required string Email { get; init; }
}
