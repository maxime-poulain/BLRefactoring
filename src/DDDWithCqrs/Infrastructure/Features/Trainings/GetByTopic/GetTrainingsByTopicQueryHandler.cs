using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTopic;
using BLRefactoring.DDDWithCqrs.Application.Pagination;
using BLRefactoring.DDDWithCqrs.Infrastructure.Pagination;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainings.GetByTopic;

public class GetTrainingsByTopicQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetTrainingsByTopicQuery, PagedResult<TrainingDto>>
{
    public async ValueTask<PagedResult<TrainingDto>> Handle(
        GetTrainingsByTopicQuery request,
        CancellationToken cancellationToken)
    {
        return await trainingContext.Trainings
            .Where(training => training.Topics.Any(topic => topic.Name == request.Topic))
            .NewestFirst<Training, TrainingId>()
            .ToPagedResultAsync(TrainingProjections.ToDtoExpression, request, cancellationToken);
    }
}
