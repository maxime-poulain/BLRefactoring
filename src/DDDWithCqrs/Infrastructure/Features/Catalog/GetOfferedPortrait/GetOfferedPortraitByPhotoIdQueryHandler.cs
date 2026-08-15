using TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetOfferedPortrait;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Catalog.GetOfferedPortrait;

/// <summary>
/// Answers the catalog's portrait by asking the port that knows which authority owns which half.
/// </summary>
/// <remarks>
/// A pass-through, like the two readers beside it, and for the reason ADR 0062 gave: the composition
/// of "on offer" exists in one place, and a reader that opened the index's table itself would be a
/// second one.
/// </remarks>
public sealed class GetOfferedPortraitByPhotoIdQueryHandler(ICatalogDetailQuery catalogDetail)
    : IQueryHandler<GetOfferedPortraitByPhotoIdQuery, TrainerPhotoDto?>
{
    /// <summary>
    /// Runs the query.
    /// </summary>
    public async ValueTask<TrainerPhotoDto?> Handle(
        GetOfferedPortraitByPhotoIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await catalogDetail.FindOfferedPortraitAsync(
            request.TrainingId, request.PhotoId, cancellationToken);
    }
}
