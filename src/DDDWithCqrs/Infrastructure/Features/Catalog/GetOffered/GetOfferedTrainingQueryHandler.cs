using TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetOffered;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Catalog.GetOffered;

/// <summary>
/// Answers the catalog's detail by asking the port that knows which authority owns which half.
/// </summary>
/// <remarks>
/// A pass-through, and for the same reason <c>SearchCatalogQueryHandler</c> is one. This reader
/// could open both <c>DbSet</c>s itself — the index's entry and the trainings table are the same
/// context's storage — and that is precisely what it must not do: the composition of "on offer"
/// would then exist here as well as in <see cref="ICatalogDetailQuery"/>, and two definitions of
/// it are what ADR 0056 spent nine consumers avoiding (ADR 0062).
/// </remarks>
public sealed class GetOfferedTrainingQueryHandler(ICatalogDetailQuery catalogDetail)
    : IQueryHandler<GetOfferedTrainingQuery, CatalogTrainingDetailDto?>
{
    /// <summary>
    /// Runs the query.
    /// </summary>
    public async ValueTask<CatalogTrainingDetailDto?> Handle(
        GetOfferedTrainingQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await catalogDetail.FindOfferedAsync(request.TrainingId, cancellationToken);
    }
}
