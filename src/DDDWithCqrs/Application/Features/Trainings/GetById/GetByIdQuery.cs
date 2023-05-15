using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetById;

public class GetTrainingByIdQuery : IQuery<TrainingDto?>
{
    public Guid Id { get; }

    public GetTrainingByIdQuery(Guid id)
    {
        Id = id;
    }
}

public class GetTrainingByIdQueryHandler : IQueryHandler<GetTrainingByIdQuery, TrainingDto?>
{
    private readonly TrainingContext _trainingContext;

    public GetTrainingByIdQueryHandler(TrainingContext trainingContext)
    {
        _trainingContext = trainingContext;
    }

    public async Task<TrainingDto?> Handle(GetTrainingByIdQuery request, CancellationToken cancellationToken)
    {
        return await _trainingContext.Trainings
            .Select(training => training.ToDto())
            .FirstOrDefaultAsync(trainingDto => trainingDto.Id == request.Id, cancellationToken);
    }
}
