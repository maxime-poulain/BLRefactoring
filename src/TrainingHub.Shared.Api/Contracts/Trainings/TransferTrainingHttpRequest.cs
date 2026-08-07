namespace TrainingHub.Shared.Api.Contracts.Trainings;

/// <summary>
/// The body of <c>POST /Training/{trainingId}/transfer</c>.
/// </summary>
/// <remarks>
/// Only the recipient is the caller's to name: the training comes from the route, and the giver
/// is whoever the <c>TrainingOwner</c> policy admitted. Whether the recipient exists, has room,
/// and is free of the title are questions for the layers behind — the attribute only refuses a
/// message that names no recipient at all (ADR 0036).
/// <para>
/// That last sentence was untrue for as long as the annotation was <c>[Required]</c>: a
/// non-nullable <see cref="Guid"/> always has a value, so <see cref="Guid.Empty"/> satisfied it and
/// travelled on, and what refused it was a validator behind the CQRS host alone.
/// <see cref="NotEmptyIdentifierAttribute"/> is what the remark always described (ADR 0046).
/// </para>
/// </remarks>
public sealed class TransferTrainingHttpRequest
{
    /// <summary>The trainer receiving the training.</summary>
    [NotEmptyIdentifier]
    public Guid RecipientTrainerId { get; init; }
}
