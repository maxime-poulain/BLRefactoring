namespace TrainingHub.Shared.Application.Queries;

/// <summary>
/// Answers whether an account has proven its email address, without loading anything.
/// </summary>
/// <remarks>
/// The authorization policy that guards creating a training needs one bit: whether the caller's
/// account is verified (ADR 0090). It reads one column of the Identity store and materializes
/// nothing, on the authorization path — <see cref="ITrainerStandingQuery"/>'s reasoning, one
/// policy over. The two boundary readers also serve the two <c>GetCurrent</c> endpoints, which
/// compose the flag into the trainer response so the browser can say what the catalog door will
/// refuse (ADR 0057).
/// <para>
/// The domain declares a port of its own with the same fact,
/// <c>ITrainerVerification.IsVerifiedAsync</c>, and the duplication is deliberate for the
/// standing pair's reason: that one settles a business rule inside the factory and speaks
/// <c>TrainerId</c>; this one refuses a request before any use case runs and speaks the token's
/// own user identifier — a boundary borrowing the domain's port would make the API depend on a
/// contract that exists for a different reason.
/// </para>
/// </remarks>
public interface IAccountVerificationQuery
{
    /// <summary>
    /// Whether the account has proven its email address.
    /// </summary>
    /// <remarks>
    /// An identifier no account answers to is not verified: the policy's job is to refuse an
    /// unproven caller, and an account that is gone has proven nothing.
    /// </remarks>
    /// <param name="userId">The account, as the token's user-id claim names it.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<bool> IsVerifiedAsync(Guid userId, CancellationToken cancellationToken = default);
}
