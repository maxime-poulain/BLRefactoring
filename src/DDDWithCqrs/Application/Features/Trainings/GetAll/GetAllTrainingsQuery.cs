using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.CQS;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetAll;

public class GetAllTrainingsQuery : IQuery<List<TrainingDto>>
{
}
