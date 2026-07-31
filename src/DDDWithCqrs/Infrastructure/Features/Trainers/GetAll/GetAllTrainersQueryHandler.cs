using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetAll;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainers.GetAll;

public class GetAllTrainersQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetAllTrainersQuery, List<TrainerDto>>
{
    public async ValueTask<List<TrainerDto>> Handle(GetAllTrainersQuery request, CancellationToken cancellationToken)
    {
        return await trainingContext.Trainers
            .Select(TrainerProjections.ToDtoExpression)
            .ToListAsync(cancellationToken);
    }
}
