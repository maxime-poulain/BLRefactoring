namespace TrainingHub.Shared.Application.Accounts;

/// <summary>
/// Records the language an account asks to be written to in.
/// </summary>
/// <remarks>
/// The writing half of the preference ADR 0091 stores, declared here for the reason the two
/// credential stores are: it writes the Identity store, which the unit of work does not own, and
/// its callers sit above the infrastructure — the registration that states the first choice, and
/// the endpoint the language selector calls when a signed-in visitor changes it.
/// <para>
/// One method, and it is an upsert: an account holds one preference, so choosing again replaces
/// the choice rather than adding to it. The caller has already checked that the code names a
/// language this application offers — the contract's own attribute does it at the boundary — so
/// nothing is validated here.
/// </para>
/// </remarks>
public interface IAccountLanguageStore
{
    /// <summary>Records the account's choice, replacing any earlier one.</summary>
    /// <param name="userId">The account stating the preference.</param>
    /// <param name="language">The neutral culture code it reads in.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task SetAsync(Guid userId, string language, CancellationToken cancellationToken = default);
}
