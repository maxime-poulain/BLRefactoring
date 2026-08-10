using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetTrainerPortrait;

/// <summary>
/// Asks for an offering trainer's portrait, on behalf of nobody in particular (ADR 0070).
/// </summary>
/// <remarks>
/// The profile's own address for the same bytes <c>GetOfferedPortraitQuery</c> reaches through a
/// training: each page asks with what it has in hand, and the trainer's identifier is what a
/// profile page has (ADR 0070). The photo identity is still what makes the answer cacheable
/// forever — a replacement mints a new one, so these bytes never change.
/// <para>
/// A <see langword="null"/> answer covers four situations and the action turns all four into a
/// 404, for the reason its neighbor gives: what a visitor is not shown, they are not told about
/// (ADR 0055).
/// </para>
/// </remarks>
public sealed class GetTrainerPortraitQuery(Guid trainerId, Guid photoId) : IQuery<TrainerPhotoDto?>
{
    /// <summary>
    /// The offering trainer the visitor is looking at.
    /// </summary>
    public Guid TrainerId { get; } = trainerId;

    /// <summary>
    /// The photo the address names.
    /// </summary>
    public Guid PhotoId { get; } = photoId;
}
