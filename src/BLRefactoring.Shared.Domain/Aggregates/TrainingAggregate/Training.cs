using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Messages;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

public sealed class Training : AggregateRoot<TrainingId>
{
    private readonly List<Topic> _topics = [];
    public IReadOnlyCollection<Topic> Topics => _topics.AsReadOnly();

    public TrainingTitle Title { get; private set; } = null!;

    public AcquiredSkills AcquiredSkills { get; private set; } = null!;

    public TrainingDescription Description { get; private set; } = null!;

    public TrainingPrerequisites Prerequisites { get; private set; } = null!;

    public TrainerId TrainerId { get; private set; } = null!;

    /// <summary>
    /// Private constructor used by the factories and by EF Core constructor
    /// binding (parameter names match the <c>Id</c> and <c>TrainerId</c> properties).
    /// </summary>
    private Training(TrainingId id, TrainerId trainerId) : base(id)
    {
        TrainerId = trainerId;
    }

    private static Training CreateDraft(TrainingId trainingId, TrainerId trainerId)
        => new(trainingId, trainerId);

    // Usually factory methods are implemented in a separate Factory class.
    // a `TrainingFactory` class would be responsible for creating Training objects.
    // This is a simplified version of a factory method. Furthermore the logic to build
    // the object is not complex enough to justify a separate class.
    // Refactoring to a factory class would not be complicated though.
    // It might have been if we were using the constructor to build the object.
    // Another good practice is to pass in an request(or message) object.
    // This encapsulates parameters and makes the code more readable.
    // Furthermore, it makes it easier to add new parameters in the future and decrease
    // the number of parameters in the method.
    public static async Task<Result<Training>> CreateAsync(
        TrainingCreationMessage trainingCreationMessage,
        IUniquenessTitleChecker titleChecker,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trainingCreationMessage);

        var training = CreateDraft(
            trainingCreationMessage.TrainingId,
            trainingCreationMessage.TrainerId);

        var applyResult = await training.ApplyEditionAsync(trainingCreationMessage, titleChecker, cancellationToken);

        return applyResult.Match(
            () =>
            {
                training.AddDomainEvent(new TrainingCreatedDomainEvent(training.Id, training.TrainerId));
                return Result<Training>.Success(training);
            },
            Result<Training>.Failure);
    }

    public async Task<Result> EditAsync(TrainingEditionMessage message,
        IUniquenessTitleChecker titleChecker,
        CancellationToken cancellationToken = default)
    {
        var result = await ApplyEditionAsync(message, titleChecker, cancellationToken);

        return result.Tap(() => AddDomainEvent(new TrainingEditedDomainEvent(Id, TrainerId)));
    }

    /// <summary>
    /// Validates the whole <paramref name="message"/> and, only when every input is
    /// valid, mutates the aggregate. Raises no domain event: the callers each raise
    /// the event matching their intent (created vs edited).
    /// </summary>
    private async Task<Result> ApplyEditionAsync(TrainingEditionMessage message,
        IUniquenessTitleChecker titleChecker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(titleChecker);

        var errors = new ErrorCollection();

        var titleResult = TrainingTitle.Create(message.Title)
            .TapError(errs => errors.AddErrors(errs));

        if (!titleResult.HasErrors())
        {
            var title = titleResult.Match(trainingTitle => trainingTitle, _ => null!);
            if (title != Title)
            {
                var titleExists = await titleChecker.TitleForTrainerExists(title, TrainerId, cancellationToken);
                if (titleExists)
                {
                    errors.Add(new Error(ErrorCode.DuplicateTitle,
                        "A training with the same title already exists for this trainer."));
                }
            }
        }

        var descriptionResult = TrainingDescription.Create(message.Description)
            .TapError(errs => errors.AddErrors(errs));

        var prerequisitesResult = TrainingPrerequisites.Create(message.Prerequisites)
            .TapError(errs => errors.AddErrors(errs));

        var acquiredSkillsResult = AcquiredSkills.Create(message.AcquiredSkills)
            .TapError(errs => errors.AddErrors(errs));

        // Topics are resolved during the validation phase, like every other input:
        // an unknown name is a validation error, never an exception, and the
        // aggregate is only mutated once the whole message has been validated.
        var topics = new List<Topic>();
        foreach (var topicName in message.Topics)
        {
            if (!Topic.TryFromName(topicName, out var topic))
            {
                errors.Add(new Error(ErrorCode.InvalidTopic, $"Topic '{topicName}' does not exist."));
            }
            else if (!topics.Contains(topic))
            {
                topics.Add(topic);
            }
        }

        if (errors.Any())
        {
            return Result.Failure(errors);
        }

        titleResult.Tap(title => Title = title);
        descriptionResult.Tap(desc => Description = desc);
        prerequisitesResult.Tap(prereq => Prerequisites = prereq);
        acquiredSkillsResult.Tap(skills => AcquiredSkills = skills);

        _topics.Clear();
        _topics.AddRange(topics);

        return Result.Success();
    }
}
