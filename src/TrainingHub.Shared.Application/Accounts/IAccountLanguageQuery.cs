namespace TrainingHub.Shared.Application.Accounts;

/// <summary>
/// Reads the language an account asked to be written to in.
/// </summary>
/// <remarks>
/// The port ADR 0091 adds for the notices whose recipient no read port already resolves — the
/// welcome, the warning to a former contact address, the erasure receipt, the password-changed
/// notice. The six others read the language beside the address they were already reading: on the
/// fact, on the invitation the token store minted, or in the projection
/// <c>ITrainerAccountQuery</c> and <c>ITrainerContactQuery</c> return. This one exists so that
/// those four can put the language on the fact they publish, which is the only place a consumer
/// could read it from once the account is gone.
/// <para>
/// It answers <see langword="null"/> for an account that has chosen no language, and passes that
/// silence on rather than filling it: the fallback belongs to the composer, which is the one
/// corner of this application allowed to name the list of languages at all (ADR 0088, ADR 0090).
/// A port that substituted a default here would be a second place to change the day the default
/// changes, and the first place a reader would fail to look.
/// </para>
/// </remarks>
public interface IAccountLanguageQuery
{
    /// <summary>The language the account behind a user identifier reads in, if it chose one.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<string?> ByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The language the account behind a trainer reads in, if it chose one.</summary>
    /// <remarks>
    /// The trainer is a row of the business store and the preference a row of the Identity store,
    /// so this crosses the same seam <c>ITrainerAccountQuery</c> does and pays the same two
    /// round-trips for the same separation.
    /// </remarks>
    /// <param name="trainerId">The trainer whose account is asked.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<string?> ByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default);
}
