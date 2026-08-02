namespace BLRefactoring.Shared.Application.Queries;

/// <summary>
/// The three facts about a trainer that a freshly issued token carries.
/// </summary>
/// <remarks>
/// Named after what the token needs rather than after the aggregate, because it is deliberately
/// less than the aggregate: adding a field to <c>Trainer</c> should not silently widen what every
/// signed-in caller carries in their claims.
/// </remarks>
/// <param name="TrainerId">Published as the <c>trainer_id</c> claim, which the ownership policy reads back.</param>
/// <param name="Firstname">Published as <c>firstname</c>.</param>
/// <param name="Lastname">Published as <c>lastname</c>.</param>
public sealed record TrainerIdentityDto(Guid TrainerId, string Firstname, string Lastname);

/// <summary>
/// Reads the trainer behind an Identity user, for the claims a token is built from.
/// </summary>
/// <remarks>
/// Same reasoning as <see cref="ITrainingOwnerQuery"/>: signing in used to load the whole
/// <c>Trainer</c> aggregate through a domain repository to copy three strings out of it. Nothing
/// on the token path mutates a trainer, so nothing on it needs an aggregate — and the API layer
/// needs no domain port at all once this one exists.
/// </remarks>
public interface ITrainerIdentityQuery
{
    /// <summary>
    /// The trainer registered against that Identity user, or <see langword="null"/> when the user
    /// has none.
    /// </summary>
    Task<TrainerIdentityDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
