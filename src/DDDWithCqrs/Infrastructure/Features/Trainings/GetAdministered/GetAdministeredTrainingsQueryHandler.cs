using TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetAdministered;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.Projections;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Infrastructure.Pagination;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Trainings.GetAdministered;

/// <summary>
/// Projects one page of trainings across every trainer, filtered in the database rather than after
/// it.
/// </summary>
/// <remarks>
/// The sibling reader <c>GetMyTrainingsQueryHandler</c> opens with a <c>Where</c> on the caller's
/// own identifier, and this one has no such line — deliberately, and it is the only reader here of
/// which that is true. Reading across every trainer is what ADR 0055 gives back to one role, and
/// the absence of that <c>Where</c> is where the decision actually lives.
/// <para>
/// Everything else follows the sibling: the filter composed before <c>ToPagedResultAsync</c> so the
/// count and the page describe the same set, columns projected rather than aggregates loaded, and
/// no specification touched (ADR 0028).
/// </para>
/// <para>
/// One filter, where the trainers' reader has two. A title is value-converted, and EF Core cannot
/// look inside a converted property — checked, not assumed (ADR 0055).
/// </para>
/// </remarks>
public sealed class GetAdministeredTrainingsQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetAdministeredTrainingsQuery, PagedResult<AdministrationTrainingDto>>
{
    /// <summary>
    /// Runs the query.
    /// </summary>
    public async ValueTask<PagedResult<AdministrationTrainingDto>> Handle(
        GetAdministeredTrainingsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trainings = trainingContext.Trainings.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = TrainingStatus.FromName(request.Status);
            trainings = trainings.Where(training => training.Status == status);
        }

        return await trainings
            .NewestFirst<Training, TrainingId>()
            .ToPagedResultAsync(
                TrainingProjections.ToAdministrationDtoExpression, request.Paging, cancellationToken);
    }
}
