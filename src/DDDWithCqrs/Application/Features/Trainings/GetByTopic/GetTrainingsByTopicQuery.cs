using BLRefactoring.DDDWithCqrs.Application.Pagination;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.CQS;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTopic;

/// <summary>
/// One page of the trainings carrying a topic, newest first.
/// </summary>
public sealed class GetTrainingsByTopicQuery(string topic) : PagedQuery, IQuery<PagedResult<TrainingDto>>
{
    public string Topic { get; } = topic;
}
