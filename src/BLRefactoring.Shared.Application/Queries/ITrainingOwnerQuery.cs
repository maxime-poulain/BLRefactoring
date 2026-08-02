namespace BLRefactoring.Shared.Application.Queries;

/// <summary>
/// Answers who owns a training, without loading the training.
/// </summary>
/// <remarks>
/// The authorization policy that guards every write to a training needs one field: the identifier
/// of the trainer who owns it. It used to get that field by asking <c>ITrainingRepository</c> for
/// the aggregate — which materialises the training, its title, its description, its prerequisites,
/// its acquired skills and its topics, on the authorization path, before the action that will read
/// the very same aggregate a second time.
/// <para>
/// A repository hands out aggregates because callers are going to change them. Nothing on this
/// path changes anything, so a repository was the wrong instrument, and reaching for it also put a
/// domain port in the hands of the API layer — the one direction the dependency rule does not
/// allow. This is the read side of that question, and it belongs to the application layer where
/// the projections already live.
/// </para>
/// </remarks>
public interface ITrainingOwnerQuery
{
    /// <summary>
    /// The identifier of the trainer who owns the training, or <see langword="null"/> when no
    /// training carries that identifier.
    /// </summary>
    /// <remarks>
    /// The null is load-bearing and must not be collapsed into "not the owner". A training that
    /// does not exist satisfies the ownership requirement on purpose, so the action can answer 404
    /// rather than 403 — refusing here would turn "not found" into a lie about permissions.
    /// </remarks>
    Task<Guid?> GetOwnerIdAsync(Guid trainingId, CancellationToken cancellationToken = default);
}
