using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

namespace TrainingHub.Shared.Application.Factories;

/// <summary>
/// Turns the primitives carried by an incoming request or command into the value
/// objects of the training aggregate, reporting every problem at once.
/// </summary>
/// <remarks>
/// Translating a message into domain concepts is an application-layer concern: the
/// domain exposes value objects and knows nothing about the shape of what the API
/// received. Both stacks and both use cases (creation and edition) go through here,
/// so the rules cannot drift between them.
/// </remarks>
public static class TrainingDetailsFactory
{
    /// <summary>
    /// Builds the value objects describing a training.
    /// </summary>
    /// <param name="title">The raw title.</param>
    /// <param name="description">The raw description.</param>
    /// <param name="prerequisites">The raw prerequisites.</param>
    /// <param name="acquiredSkills">The raw acquired skills.</param>
    /// <param name="topicNames">
    /// The raw topic names. Topics form a closed set owned by the domain, so an
    /// unknown name is a validation error rather than an exception — resolving it
    /// here keeps the aggregate free of any string parsing.
    /// </param>
    public static Result<TrainingDetails> Create(
        string title,
        string description,
        string prerequisites,
        string acquiredSkills,
        IEnumerable<string> topicNames)
    {
        ArgumentNullException.ThrowIfNull(topicNames);

        var errors = new ErrorCollection();

        TrainingTitle? trainingTitle = null;
        TrainingDescription? trainingDescription = null;
        TrainingPrerequisites? trainingPrerequisites = null;
        AcquiredSkills? skills = null;

        TrainingTitle.Create(title).Switch(value => trainingTitle = value, errors.AddErrors);
        TrainingDescription.Create(description).Switch(value => trainingDescription = value, errors.AddErrors);
        TrainingPrerequisites.Create(prerequisites).Switch(value => trainingPrerequisites = value, errors.AddErrors);
        AcquiredSkills.Create(acquiredSkills).Switch(value => skills = value, errors.AddErrors);

        var topics = new List<Topic>();
        foreach (var topicName in topicNames)
        {
            if (!Topic.TryFromName(topicName, out var topic))
            {
                errors.Add(new Error(TrainingErrorCodes.InvalidTopic, $"Topic '{topicName}' does not exist."));
            }
            else
            {
                topics.Add(topic);
            }
        }

        return errors.Any()
            ? Result<TrainingDetails>.Failure(errors)
            : Result<TrainingDetails>.Success(new TrainingDetails(
                trainingTitle!, trainingDescription!, trainingPrerequisites!, skills!, topics));
    }
}

/// <summary>
/// The validated value objects describing a training, as handed from the factory to
/// the caller that will pass them on to the aggregate.
/// </summary>
/// <remarks>
/// An application-layer carrier, not a domain concept: it exists only so the factory
/// can return five value objects at once without an unreadable tuple. The domain
/// never references it — <c>Training</c> takes the value objects themselves.
/// </remarks>
public sealed record TrainingDetails(
    TrainingTitle Title,
    TrainingDescription Description,
    TrainingPrerequisites Prerequisites,
    AcquiredSkills AcquiredSkills,
    IReadOnlyCollection<Topic> Topics);
