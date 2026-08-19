using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Auth;

/// <summary>
/// The body of <c>PUT /Auth/language</c>: the language the calling account wants to be written to
/// in.
/// </summary>
/// <remarks>
/// The account's preference, not the session's: a visitor reading one page in another language has
/// not asked for their suspension notice in it, which is why changing it is a request of its own
/// rather than a side effect of the culture cookie (ADR 0091). The front end sends both together
/// from the language selector, because that is where a person looks for this and the profile form
/// has no business carrying a second copy.
/// </remarks>
public sealed class SetAccountLanguageHttpRequest
{
    /// <summary>
    /// The neutral culture code — <c>en</c>, <c>fr</c>, <c>ru</c> — the account reads in.
    /// </summary>
    [Required]
    [KnownLanguage]
    public required string Language { get; init; }
}
