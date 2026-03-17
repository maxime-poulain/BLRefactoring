using BLRefactoring.Shared.Application.Dtos;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTrainerId;

public class GetTrainingsByTrainerIdQuery(Guid trainerId) : IQuery<List<TrainingDto>>
{
    public Guid TrainerId { get; } = trainerId;
}

public class GetTrainingsByTrainerIdQueryHandler(ITrainingRepository trainingRepository)
    : IQueryHandler<GetTrainingsByTrainerIdQuery, List<TrainingDto>>
{
    public async ValueTask<List<TrainingDto>> Handle(
        GetTrainingsByTrainerIdQuery request,
        CancellationToken cancellationToken)
    {
        var trainings = await trainingRepository.GetByTrainerIdAsync(
            (TrainerId)request.TrainerId, cancellationToken);

        return trainings.ToDtos();
    }
}
