using BLRefactoring.DDDWithCqrs.Application.Features.Trainers;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetById;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainers.GetById;

public class GetTrainerByIdQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetTrainerByIdQuery, TrainerDto?>
{
    public async ValueTask<TrainerDto?> Handle(GetTrainerByIdQuery request, CancellationToken cancellationToken)
    {
        var trainerId = TrainerId.Create(request.Id);

        return await trainingContext.Trainers
            .Where(trainer => trainer.Id == trainerId)
            .Select(TrainerProjections.ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
