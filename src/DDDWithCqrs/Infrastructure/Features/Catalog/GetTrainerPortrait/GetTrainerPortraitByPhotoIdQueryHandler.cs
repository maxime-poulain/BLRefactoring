using TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetTrainerPortrait;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Catalog.GetTrainerPortrait;

/// <summary>
/// Answers the profile's portrait by asking the port that knows which authority owns which half.
/// </summary>
/// <remarks>
/// A pass-through, like the readers beside it, and for the reason ADR 0062 gave: the composition
/// of "on offer" exists in one place, and a reader that opened the index's table itself would be a
/// second one.
/// </remarks>
public sealed class GetTrainerPortraitByPhotoIdQueryHandler(ICatalogDetailQuery catalogDetail)
    : IQueryHandler<GetTrainerPortraitByPhotoIdQuery, TrainerPhotoDto?>
{
    /// <summary>
    /// Runs the query.
    /// </summary>
    public async ValueTask<TrainerPhotoDto?> Handle(
        GetTrainerPortraitByPhotoIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await catalogDetail.FindTrainerPortraitAsync(
            request.TrainerId, request.PhotoId, cancellationToken);
    }
}
