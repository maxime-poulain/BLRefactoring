using TrainingHub.DDDWithCqrs.Application.Features.Catalogue.GetOfferedPortrait;
using TrainingHub.Shared.Application.Catalogue;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Catalogue.GetOfferedPortrait;

/// <summary>
/// Answers the catalogue's portrait by asking the port that knows which authority owns which half.
/// </summary>
/// <remarks>
/// A pass-through, like the two readers beside it, and for the reason ADR 0062 gave: the composition
/// of "on offer" exists in one place, and a reader that opened the index's table itself would be a
/// second one.
/// </remarks>
public sealed class GetOfferedPortraitQueryHandler(ICatalogueDetailQuery catalogueDetail)
    : IQueryHandler<GetOfferedPortraitQuery, TrainerPhotoDto?>
{
    /// <summary>
    /// Runs the query.
    /// </summary>
    public async ValueTask<TrainerPhotoDto?> Handle(
        GetOfferedPortraitQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await catalogueDetail.FindOfferedPortraitAsync(
            request.TrainingId, request.PhotoId, cancellationToken);
    }
}
