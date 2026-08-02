using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
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
    // The method takes value objects rather than a parameter object of primitives:
    // the shape of what the application layer receives is none of the domain's
    // business, and every part is already valid by the time it gets here.
    public static async Task<Result<Training>> CreateAsync(
        TrainingId trainingId,
        TrainerId trainerId,
        TrainingTitle title,
        TrainingDescription description,
        TrainingPrerequisites prerequisites,
        AcquiredSkills acquiredSkills,
        IReadOnlyCollection<Topic> topics,
        IUniquenessTitleChecker titleChecker,
        CancellationToken cancellationToken = default)
    {
        var training = CreateDraft(trainingId, trainerId);

        var applyResult = await training.ApplyEditionAsync(
            title, description, prerequisites, acquiredSkills, topics, titleChecker, cancellationToken);

        return applyResult.Match(
            () =>
            {
                training.AddDomainEvent(new TrainingCreatedDomainEvent(training.Id, training.TrainerId));
                return Result<Training>.Success(training);
            },
            Result<Training>.Failure);
    }

    public async Task<Result> EditAsync(
        TrainingTitle title,
        TrainingDescription description,
        TrainingPrerequisites prerequisites,
        AcquiredSkills acquiredSkills,
        IReadOnlyCollection<Topic> topics,
        IUniquenessTitleChecker titleChecker,
        CancellationToken cancellationToken = default)
    {
        var result = await ApplyEditionAsync(
            title, description, prerequisites, acquiredSkills, topics, titleChecker, cancellationToken);

        return result.Tap(() => AddDomainEvent(new TrainingEditedDomainEvent(Id, TrainerId)));
    }

    /// <summary>
    /// Applies the given content and, only when the title is available, mutates the
    /// aggregate. Raises no domain event: the callers each raise the event matching
    /// their intent (created vs edited).
    /// </summary>
    /// <remarks>
    /// Every argument is a value object, so nothing here can be malformed. The one
    /// rule left to enforce is the only one the aggregate cannot decide on its own:
    /// a title must be unique among the trainings of the same trainer.
    /// </remarks>
    private async Task<Result> ApplyEditionAsync(
        TrainingTitle title,
        TrainingDescription description,
        TrainingPrerequisites prerequisites,
        AcquiredSkills acquiredSkills,
        IReadOnlyCollection<Topic> topics,
        IUniquenessTitleChecker titleChecker,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(prerequisites);
        ArgumentNullException.ThrowIfNull(acquiredSkills);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(titleChecker);

        if (title != Title)
        {
            var titleExists = await titleChecker.TitleForTrainerExistsAsync(title, TrainerId, cancellationToken);
            if (titleExists)
            {
                return Result.Failure(TrainingErrorCodes.DuplicateTitle,
                    "A training with the same title already exists for this trainer.");
            }
        }

        Title = title;
        Description = description;
        Prerequisites = prerequisites;
        AcquiredSkills = acquiredSkills;

        _topics.Clear();
        _topics.AddRange(topics.Distinct());

        return Result.Success();
    }
}
