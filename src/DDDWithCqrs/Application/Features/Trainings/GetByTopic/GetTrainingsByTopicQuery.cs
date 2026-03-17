using BLRefactoring.Shared.Application.Dtos;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTopic;

public class GetTrainingsByTopicQuery(string topic) : IQuery<List<TrainingDto>>
{
    public string Topic { get; } = topic;
}

public class GetTrainingsByTopicQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetTrainingsByTopicQuery, List<TrainingDto>>
{
    public async ValueTask<List<TrainingDto>> Handle(
        GetTrainingsByTopicQuery request,
        CancellationToken cancellationToken)
    {
        return await trainingContext.Trainings
            .Where(t => t.Topics.Any(topic => topic.Name == request.Topic))
            .Select(t => t.ToDto())
            .ToListAsync(cancellationToken);
    }
}
