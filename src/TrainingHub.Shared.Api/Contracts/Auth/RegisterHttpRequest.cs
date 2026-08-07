using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Auth;

/// <summary>
/// The body of <c>POST /Auth/register</c>: an account to open and the trainer behind it.
/// </summary>
/// <remarks>
/// The only request in this API that writes to two contexts. Both halves commit or neither does
/// (ADR 0040), which is why one body carries the account's credentials and the trainer's name.
/// <para>
/// It lived beside <c>AuthControllerBase</c> until ADR 0042, unnamed as a contract and therefore
/// invisible to the rule that keeps contracts on the boundary — while being as published as any
/// of them, in the generated client and consumed by the Blazor front.
/// </para>
/// <para>
/// The name is bounded here, exactly as <c>EditTrainerHttpRequest</c> bounds it, so that the two
/// paths into a <c>Trainer</c> refuse the same names. What is deliberately absent is any check on
/// the shape of <see cref="Email"/>: .NET's <c>[EmailAddress]</c> and the domain's validator
/// disagree in both directions — the domain accepts a quoted local part containing an <c>@</c>,
/// and refuses <c>a@b</c>, which .NET accepts — so the boundary declares presence and lets the
/// domain judge the address (ADR 0043).
/// </para>
/// </remarks>
public sealed class RegisterHttpRequest
{
    /// <summary>
    /// The username of the new user.
    /// </summary>
    [Required]
    public required string Username { get; init; }

    /// <summary>
    /// The email address of the new user, which also becomes the trainer's contact address.
    /// </summary>
    [Required]
    public required string Email { get; init; }

    /// <summary>
    /// The password for the new user.
    /// </summary>
    [Required]
    public required string Password { get; init; }

    /// <summary>
    /// The confirmation of the password.
    /// </summary>
    [Required]
    public required string ConfirmPassword { get; init; }

    /// <summary>
    /// The first name of the new user.
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public required string Firstname { get; init; }

    /// <summary>
    /// The last name of the new user.
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public required string Lastname { get; init; }
}
