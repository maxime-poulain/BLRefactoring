using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetAll;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainings.GetAll;

public class GetAllTrainingsQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetAllTrainingsQuery, List<TrainingDto>>
{
    public async ValueTask<List<TrainingDto>> Handle(GetAllTrainingsQuery request, CancellationToken cancellationToken)
    {
        // In real life use pagination.
        return await trainingContext.Trainings
            .Select(TrainingProjections.ToDtoExpression)
            .ToListAsync(cancellationToken);
    }
}
