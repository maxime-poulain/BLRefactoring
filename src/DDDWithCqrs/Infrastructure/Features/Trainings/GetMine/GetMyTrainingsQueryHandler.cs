using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetMine;
using BLRefactoring.DDDWithCqrs.Application.Pagination;
using BLRefactoring.DDDWithCqrs.Infrastructure.Pagination;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainings.GetMine;

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
