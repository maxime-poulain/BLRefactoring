using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Auth;

/// <summary>
/// The body of <c>POST /Auth/verify-email</c>: the token the emailed verification link carried.
/// </summary>
/// <remarks>
/// In the body rather than in the query on purpose: a query string lands in access logs, and a
/// live credential has no business in one — the link itself carries the token as a query only as
/// far as the verification page, which posts it here (ADR 0090). Presence is the only thing the
/// boundary checks: any malformed token digests to something that matches nothing, and the one
/// dead-link sentence answers it like every other dead link (ADR 0084's discipline).
/// </remarks>
public sealed class VerifyEmailHttpRequest
{
    /// <summary>
    /// The verification token, exactly as the emailed link carried it.
    /// </summary>
    [Required]
    public required string Token { get; init; }
}
