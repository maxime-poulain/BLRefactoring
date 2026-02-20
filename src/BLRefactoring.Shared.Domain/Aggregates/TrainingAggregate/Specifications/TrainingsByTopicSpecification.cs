using BLRefactoring.Shared.Common.Specifications;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Specifications;

/// <summary>
/// Specification that filters trainings by a given topic name.
/// Matches trainings that contain at least one topic with the specified name.
/// </summary>
public sealed class TrainingsByTopicSpecification : Specification<Training>
{
    /// <summary>
    /// Initializes a new instance of <see cref="TrainingsByTopicSpecification"/>
    /// that matches trainings containing the specified topic.
    /// </summary>
    /// <param name="topicName">The name of the topic to filter by.</param>
    public TrainingsByTopicSpecification(string topicName)
    {
        AddCriteria(training => training.Topics.Any(t => t.Name == topicName));
    }
}
