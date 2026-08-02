using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetById;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainings.GetById;

public sealed class GetTrainingByIdQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetTrainingByIdQuery, TrainingDto?>
{
    public async ValueTask<TrainingDto?> Handle(GetTrainingByIdQuery request, CancellationToken cancellationToken)
    {
        var trainingId = TrainingId.Create(request.Id);

        return await trainingContext.Trainings
            .Where(training => training.Id == trainingId)
            .Select(TrainingProjections.ToDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
