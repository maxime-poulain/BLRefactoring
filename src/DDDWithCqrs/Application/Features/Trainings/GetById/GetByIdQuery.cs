using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetById;

public class GetTrainingByIdQuery(Guid id) : IQuery<TrainingDto?>
{
    public Guid Id { get; } = id;
}

public class GetTrainingByIdQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetTrainingByIdQuery, TrainingDto?>
{
    public async ValueTask<TrainingDto?> Handle(GetTrainingByIdQuery request, CancellationToken cancellationToken)
    {
        return await trainingContext.Trainings
            .Select(training => training.ToDto())
            .FirstOrDefaultAsync(trainingDto => trainingDto.Id == request.Id, cancellationToken);
    }
}
