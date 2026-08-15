using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetOfferedPortrait;

/// <summary>
/// Asks for the portrait behind an offered training, on behalf of nobody in particular (ADR 0063).
/// </summary>
/// <remarks>
/// It carries a training and a photo, and no trainer — which is the whole shape of the decision.
/// The visitor followed a catalog entry, so the training is what they have; the photo identity is
/// what makes the answer cacheable forever; and a person's identifier has no business in a message
/// built from a public address.
/// <para>
/// A <see langword="null"/> answer covers four situations and the action turns all four into a 404,
/// for the reason its neighbor gives: what a visitor is not shown, they are not told about
/// (ADR 0055).
/// </para>
/// </remarks>
public sealed class GetOfferedPortraitByPhotoIdQuery(Guid trainingId, Guid photoId) : IQuery<TrainerPhotoDto?>
{
    /// <summary>
    /// The offered training the visitor is looking at.
    /// </summary>
    public Guid TrainingId { get; } = trainingId;

    /// <summary>
    /// The photo the address names.
    /// </summary>
    public Guid PhotoId { get; } = photoId;
}
