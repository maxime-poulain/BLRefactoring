using BLRefactoring.DDDWithCqrs.Application.Pagination;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.CQS;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetAll;

/// <summary>
/// One page of trainers, newest first.
/// </summary>
public class GetAllTrainersQuery : PagedQuery, IQuery<PagedResult<TrainerDto>>
{
}
