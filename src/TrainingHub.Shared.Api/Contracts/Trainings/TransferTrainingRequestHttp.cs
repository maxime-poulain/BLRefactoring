using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Trainings;

/// <summary>
/// The body of <c>POST /Training/{trainingId}/transfer</c>.
/// </summary>
/// <remarks>
/// Only the recipient is the caller's to name: the training comes from the route, and the giver
/// is whoever the <c>TrainingOwner</c> policy admitted. Whether the recipient exists, has room,
/// and is free of the title are questions for the layers behind — the attribute only refuses a
/// message with no recipient at all (ADR 0036).
/// </remarks>
public sealed class TransferTrainingRequestHttp
{
    /// <summary>The trainer receiving the training.</summary>
    [Required]
    public Guid RecipientTrainerId { get; init; }
}
