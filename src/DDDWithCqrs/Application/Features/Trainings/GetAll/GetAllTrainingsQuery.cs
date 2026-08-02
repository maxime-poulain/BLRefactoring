using BLRefactoring.DDDWithCqrs.Application.Pagination;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.CQS;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetAll;

/// <summary>
/// One page of trainings, newest first.
/// </summary>
public sealed class GetAllTrainingsQuery : PagedQuery, IQuery<PagedResult<TrainingDto>>
{
}
