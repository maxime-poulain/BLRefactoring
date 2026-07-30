using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.CQS;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetById;

public class GetTrainingByIdQuery(Guid id) : IQuery<TrainingDto?>
{
    public Guid Id { get; } = id;
}
