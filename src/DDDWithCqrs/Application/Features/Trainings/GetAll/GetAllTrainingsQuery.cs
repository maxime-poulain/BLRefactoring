using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetAll;

public class GetAllTrainingsQuery : IQuery<List<TrainingDto>>
{
}

public class GetAllTrainingQueryHandler : IQueryHandler<GetAllTrainingsQuery, List<TrainingDto>>
{
    private readonly TrainingContext _trainingContext;

    public GetAllTrainingQueryHandler(TrainingContext trainingContext)
    {
        _trainingContext = trainingContext;
    }

    public async ValueTask<List<TrainingDto>> Handle(GetAllTrainingsQuery request, CancellationToken cancellationToken)
    {
        // In real life use pagination.
        return await _trainingContext.Trainings
            .Select(training => new TrainingDto()
            {
                Rates = training.Rates.ToDtos(),
                Id = training.Id,
                StartDate = training.StartDate,
                EndDate = training.EndDate,
                TrainerId = training.TrainerId,
                Title = training.Title
            }).ToListAsync(cancellationToken);
    }
}
