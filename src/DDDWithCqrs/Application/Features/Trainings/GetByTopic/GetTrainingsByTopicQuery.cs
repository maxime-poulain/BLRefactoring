using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.CQS;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTopic;

public class GetTrainingsByTopicQuery(string topic) : IQuery<List<TrainingDto>>
{
    public string Topic { get; } = topic;
}
