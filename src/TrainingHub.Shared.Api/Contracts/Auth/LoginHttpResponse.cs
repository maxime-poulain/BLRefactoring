namespace TrainingHub.Shared.Api.Contracts.Auth;

/// <summary>
/// The body of a successful <c>POST /Auth/login</c>: the token to present afterwards.
/// </summary>
/// <remarks>
/// The token is the whole answer. Nothing about the account travels with it — the claims inside
/// say who the caller is, and the trainer they map to is read from the token by
/// <c>ICurrentUserService</c> rather than handed out here.
/// </remarks>
public sealed class LoginHttpResponse
{
    /// <summary>
    /// The JWT token generated for the authenticated user.
    /// </summary>
    public required string Token { get; init; }
}
