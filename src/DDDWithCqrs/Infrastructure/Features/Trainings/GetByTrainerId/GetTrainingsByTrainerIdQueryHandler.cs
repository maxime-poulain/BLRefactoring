using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTrainerId;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainings.GetByTrainerId;

public class GetTrainingsByTrainerIdQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetTrainingsByTrainerIdQuery, List<TrainingDto>>
{
    public async ValueTask<List<TrainingDto>> Handle(
        GetTrainingsByTrainerIdQuery request,
        CancellationToken cancellationToken)
    {
        var trainerId = TrainerId.Create(request.TrainerId);

        return await trainingContext.Trainings
            .Where(training => training.TrainerId == trainerId)
            .Select(TrainingProjections.ToDto)
            .ToListAsync(cancellationToken);
    }
}
