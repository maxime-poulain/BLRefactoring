using BLRefactoring.DDDWithCqrs.Application.Pagination;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.CQS;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTrainerId;

/// <summary>
/// One page of a given trainer's trainings, newest first.
/// </summary>
public class GetTrainingsByTrainerIdQuery(Guid trainerId) : PagedQuery, IQuery<PagedResult<TrainingDto>>
{
    public Guid TrainerId { get; } = trainerId;
}
