using TrainingHub.DDDWithCqrs.Application.Features.Catalogue.GetOffered;
using TrainingHub.Shared.Application.Catalogue;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Catalogue.GetOffered;

/// <summary>
/// Answers the catalogue's detail by asking the port that knows which authority owns which half.
/// </summary>
/// <remarks>
/// A pass-through, and for the same reason <c>SearchCatalogueQueryHandler</c> is one. This reader
/// could open both <c>DbSet</c>s itself — the index's entry and the trainings table are the same
/// context's storage — and that is precisely what it must not do: the composition of "on offer"
/// would then exist here as well as in <see cref="ICatalogueDetailQuery"/>, and two definitions of
/// it are what ADR 0056 spent nine consumers avoiding (ADR 0062).
/// </remarks>
public sealed class GetOfferedTrainingQueryHandler(ICatalogueDetailQuery catalogueDetail)
    : IQueryHandler<GetOfferedTrainingQuery, CatalogueTrainingDetailDto?>
{
    /// <summary>
    /// Runs the query.
    /// </summary>
    public async ValueTask<CatalogueTrainingDetailDto?> Handle(
        GetOfferedTrainingQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await catalogueDetail.FindOfferedAsync(request.TrainingId, cancellationToken);
    }
}
