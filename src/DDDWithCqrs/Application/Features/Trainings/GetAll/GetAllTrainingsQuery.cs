using BLRefactoring.Shared.Application.Dtos;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetAll;

public class GetAllTrainingsQuery : IQuery<List<TrainingDto>>
{
}

public class GetAllTrainingQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetAllTrainingsQuery, List<TrainingDto>>
{
    public async ValueTask<List<TrainingDto>> Handle(GetAllTrainingsQuery request, CancellationToken cancellationToken)
    {
        // In real life use pagination.
        return await trainingContext.
            Trainings
            .Select(x => x.ToDto())
            .ToListAsync(cancellationToken);
    }
}
