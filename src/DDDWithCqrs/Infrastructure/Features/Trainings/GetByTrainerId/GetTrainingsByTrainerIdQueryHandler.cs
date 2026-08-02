using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTrainerId;
using BLRefactoring.DDDWithCqrs.Application.Pagination;
using BLRefactoring.DDDWithCqrs.Infrastructure.Pagination;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainings.GetByTrainerId;

public sealed class GetTrainingsByTrainerIdQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetTrainingsByTrainerIdQuery, PagedResult<TrainingDto>>
{
    public async ValueTask<PagedResult<TrainingDto>> Handle(
        GetTrainingsByTrainerIdQuery request,
        CancellationToken cancellationToken)
    {
        var trainerId = TrainerId.Create(request.TrainerId);

        // Filter first, then order: the WHERE narrows what has to be sorted and paged.
        return await trainingContext.Trainings
            .Where(training => training.TrainerId == trainerId)
            .NewestFirst<Training, TrainingId>()
            .ToPagedResultAsync(TrainingProjections.ToDtoExpression, request, cancellationToken);
    }
}
