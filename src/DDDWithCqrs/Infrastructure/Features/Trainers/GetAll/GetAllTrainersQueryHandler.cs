using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetAll;
using BLRefactoring.DDDWithCqrs.Application.Pagination;
using BLRefactoring.DDDWithCqrs.Infrastructure.Pagination;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainers.GetAll;

public sealed class GetAllTrainersQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetAllTrainersQuery, PagedResult<TrainerDto>>
{
    public async ValueTask<PagedResult<TrainerDto>> Handle(
        GetAllTrainersQuery request,
        CancellationToken cancellationToken)
    {
        return await trainingContext.Trainers
            .NewestFirst<Trainer, TrainerId>()
            .ToPagedResultAsync(TrainerProjections.ToDtoExpression, request, cancellationToken);
    }
}
