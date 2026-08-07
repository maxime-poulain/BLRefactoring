using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Auth;

/// <summary>
/// The body of <c>POST /Auth/login</c>: the credentials to present.
/// </summary>
/// <remarks>
/// Both fields are required and neither is bounded, deliberately. A length rule here would answer
/// a malformed credential differently from a wrong one, and the endpoint's whole discipline is
/// that an unknown username and a wrong password are answered identically — telling them apart
/// would make it an oracle for which accounts exist.
/// </remarks>
public sealed class LoginHttpRequest
{
    /// <summary>
    /// The username of the user attempting to log in.
    /// </summary>
    [Required]
    public required string Username { get; init; }

    /// <summary>
    /// The password of the user attempting to log in.
    /// </summary>
    [Required]
    public required string Password { get; init; }
}
