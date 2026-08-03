using TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetMine;
using TrainingHub.DDDWithCqrs.Application.Pagination;
using TrainingHub.DDDWithCqrs.Infrastructure.Pagination;
using TrainingHub.Shared;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.Projections;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Trainings.GetMine;

/// <summary>
/// Projects the calling trainer's trainings, filtered in the database rather than after it.
/// </summary>
/// <remarks>
/// The trainer is resolved here rather than received, which is what makes the query safe by
/// construction: no call site can name a different one, because there is no parameter to name it
/// with. <c>CreateTrainingCommandHandler</c> resolves the owner of a new training the same way.
/// <para>
/// The <c>Where</c> is composed before <c>ToPagedResultAsync</c>, so the filter reaches SQL and the
/// count and the page agree on the same set. Filtering a materialised page instead would page over
/// everybody's trainings and then remove most of them — a caller would receive a short page, a
/// wrong total, and rows that were read before being discarded.
/// </para>
/// </remarks>
public sealed class GetMyTrainingsQueryHandler(
    TrainingContext trainingContext,
    ICurrentUserService currentUserService)
    : IQueryHandler<GetMyTrainingsQuery, PagedResult<TrainingDto>>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<PagedResult<TrainingDto>> Handle(
        GetMyTrainingsQuery request,
        CancellationToken cancellationToken)
    {
        var trainerId = TrainerId.Create(currentUserService.TrainerId);

        return await trainingContext.Trainings
            .Where(training => training.TrainerId == trainerId)
            .NewestFirst<Training, TrainingId>()
            .ToPagedResultAsync(TrainingProjections.ToDtoExpression, request, cancellationToken);
    }
}
