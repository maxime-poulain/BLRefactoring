using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Auth;

/// <summary>
/// The body of <c>POST /Auth/erase-account</c>: the caller's own password, and nothing else.
/// </summary>
/// <remarks>
/// The account to erase comes from the token, never from the body — an erasure names nobody but
/// its caller. The password is what turns "whoever holds the tab" into "whoever owns the
/// account" (ADR 0085). No length bounds on it: like <see cref="LoginHttpRequest"/>, a shape
/// rule on a credential would answer a malformed one differently from a wrong one.
/// </remarks>
public sealed class EraseAccountHttpRequest
{
    /// <summary>
    /// The account's current password — the proof that the person asking is the person leaving.
    /// </summary>
    [Required]
    public required string Password { get; init; }
}
