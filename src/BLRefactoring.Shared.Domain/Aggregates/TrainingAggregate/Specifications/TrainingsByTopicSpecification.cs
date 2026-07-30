using BLRefactoring.Shared.Common.Specifications;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Specifications;

/// <summary>
/// Specification that filters trainings by a given topic.
/// Matches trainings that carry the specified topic.
/// </summary>
public sealed class TrainingsByTopicSpecification : Specification<Training>
{
    /// <summary>
    /// Initializes a new instance of <see cref="TrainingsByTopicSpecification"/>
    /// that matches trainings containing the specified topic.
    /// </summary>
    /// <remarks>
    /// The specification speaks in <see cref="Topic"/>, not in topic names: resolving
    /// a caller-supplied string against the closed set of topics happens before the
    /// domain is reached. Only the criteria itself unwraps the name, because that is
    /// what the persisted column holds.
    /// </remarks>
    /// <param name="topic">The topic to filter by.</param>
    public TrainingsByTopicSpecification(Topic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        var topicName = topic.Name;
        AddCriteria(training => training.Topics.Any(t => t.Name == topicName));
    }
}
