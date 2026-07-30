using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTopic;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainings.GetByTopic;

public class GetTrainingsByTopicQueryHandler(TrainingContext trainingContext)
    : IQueryHandler<GetTrainingsByTopicQuery, List<TrainingDto>>
{
    public async ValueTask<List<TrainingDto>> Handle(
        GetTrainingsByTopicQuery request,
        CancellationToken cancellationToken)
    {
        return await trainingContext.Trainings
            .Where(training => training.Topics.Any(topic => topic.Name == request.Topic))
            .Select(TrainingProjections.ToDto)
            .ToListAsync(cancellationToken);
    }
}
