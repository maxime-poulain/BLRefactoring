using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.CQS;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTrainerId;

public class GetTrainingsByTrainerIdQuery(Guid trainerId) : IQuery<List<TrainingDto>>
{
    public Guid TrainerId { get; } = trainerId;
}
