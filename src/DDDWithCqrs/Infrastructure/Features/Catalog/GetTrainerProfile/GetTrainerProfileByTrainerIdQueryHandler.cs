using TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetTrainerProfile;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Catalog.GetTrainerProfile;

/// <summary>
/// Answers the catalog's trainer profile by asking the port that knows which authority owns which
/// half.
/// </summary>
/// <remarks>
/// A pass-through, like the three readers beside it, and for the reason ADR 0062 gave: the
/// composition of "on offer" exists in one place, and a reader that opened the index's table
/// itself would be a second one.
/// </remarks>
public sealed class GetTrainerProfileByTrainerIdQueryHandler(ICatalogDetailQuery catalogDetail)
    : IQueryHandler<GetTrainerProfileByTrainerIdQuery, CatalogTrainerDto?>
{
    /// <summary>
    /// Runs the query.
    /// </summary>
    public async ValueTask<CatalogTrainerDto?> Handle(
        GetTrainerProfileByTrainerIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await catalogDetail.FindOfferedTrainerAsync(request.TrainerId, cancellationToken);
    }
}
