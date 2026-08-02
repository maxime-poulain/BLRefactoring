using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetAll;
using BLRefactoring.DDDWithCqrs.Application.Pagination;
using BLRefactoring.DDDWithCqrs.Infrastructure.Pagination;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainings.GetAll;

public sealed class GetAllTrainingsQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetAllTrainingsQuery, PagedResult<TrainingDto>>
{
    public async ValueTask<PagedResult<TrainingDto>> Handle(
        GetAllTrainingsQuery request,
        CancellationToken cancellationToken)
    {
        return await trainingContext.Trainings
            .NewestFirst<Training, TrainingId>()
            .ToPagedResultAsync(TrainingProjections.ToDtoExpression, request, cancellationToken);
    }
}
