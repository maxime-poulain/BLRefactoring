namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

/// <summary>
/// The language an account asked to be written to in.
/// </summary>
/// <remarks>
/// The third guest of the Identity store, in the mold of the two credentials beside it and for a
/// milder reason: a preference is not a secret. What it shares with them is where it belongs —
/// this is a fact about the account, not about the trainer, and the two notices that outlive a
/// trainer profile (the erasure receipt and the reset link) still need it (ADR 0091).
/// <list type="bullet">
/// <item><description>
/// <b>One row per account.</b> <see cref="UserId"/> is the primary key, so choosing again
/// replaces the choice rather than adding to it.
/// </description></item>
/// <item><description>
/// <b>The row dies with the account.</b> By cascade, like the credentials — an erased account
/// leaves no preference behind.
/// </description></item>
/// <item><description>
/// <b>Absence is not an error.</b> An account with no row reads in
/// <c>SupportedLanguages.Default</c>, which is what an account created before this table does,
/// and what a delivery does rather than fail.
/// </description></item>
/// </list>
/// The value is a neutral culture code — <c>en</c>, <c>fr</c>, <c>ru</c> — and this class checks
/// nothing about it: the set of languages this application offers is named by
/// <c>TrainingHub.Translations</c>, which the boundary that writes here validates against and this
/// ring may not reach past its <c>Email</c> corner (ADR 0088, ADR 0090).
/// </remarks>
public sealed class AccountLanguage
{
    /// <summary>
    /// Initializes the preference of an account.
    /// </summary>
    /// <param name="userId">The account the preference belongs to.</param>
    /// <param name="language">The neutral culture code the account reads in.</param>
    public AccountLanguage(Guid userId, string language)
    {
        UserId = userId;
        Language = language;
    }

    /// <summary>
    /// The account this preference belongs to — and the primary key, which is the one-choice
    /// invariant expressed as schema.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>The neutral culture code the account reads in.</summary>
    public string Language { get; private set; }

    /// <summary>Replaces the choice in place: the previous language stops being the answer.</summary>
    /// <param name="language">The neutral culture code the account now reads in.</param>
    public void Choose(string language) => Language = language;
}
