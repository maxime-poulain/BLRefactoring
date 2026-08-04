using TrainingHub.Shared.Common;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

/// <summary>
/// A training a trainer publishes.
/// <para>
/// The aggregate root of its own boundary: its constructor is private, its topics are exposed
/// read-only, and every transition goes through a behaviour method that either succeeds entirely
/// or changes nothing. It accepts value objects, never a raw <see langword="string"/> — turning
/// input into those is the application layer's job.
/// </para>
/// </summary>
public sealed class Training : AggregateRoot<TrainingId>
{
    /// <summary>
    /// How many trainings one trainer may publish: no trainer's catalogue holds more than ten.
    /// </summary>
    /// <remarks>
    /// A business rule, not a technical bound — the number is the domain expert's, and it lives
    /// here because the rule is about trainings even though it is counted per trainer, exactly
    /// as title uniqueness is. <see cref="CreateAsync"/> enforces it; nothing else reads it as
    /// permission to exist, so a catalogue that was over the limit before the rule existed keeps
    /// its trainings and merely cannot grow.
    /// </remarks>
    public const int MaximumPerTrainer = 10;

    private readonly List<Topic> _topics = [];

    /// <summary>
    /// The topics this training is filed under. Read-only: a caller changes them through a
    /// behaviour method or not at all.
    /// </summary>
    public IReadOnlyCollection<Topic> Topics => _topics.AsReadOnly();

    /// <summary>
    /// The training's title.
    /// </summary>
    public TrainingTitle Title { get; private set; } = null!;

    /// <summary>
    /// What a participant leaves with.
    /// </summary>
    public AcquiredSkills AcquiredSkills { get; private set; } = null!;

    /// <summary>
    /// The training's description.
    /// </summary>
    public TrainingDescription Description { get; private set; } = null!;

    /// <summary>
    /// What a participant needs beforehand.
    /// </summary>
    public TrainingPrerequisites Prerequisites { get; private set; } = null!;

    /// <summary>
    /// The trainer that owns this training.
    /// </summary>
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

    /// <summary>
    /// Builds a <see cref="Training"/> from raw input.
    /// </summary>
    /// <returns>
    /// The value, or every rule it broke. Failure is returned rather than thrown: a
    /// caller sending three bad fields learns about all three at once.
    /// </returns>
    public static async Task<Result<Training>> CreateAsync(
        TrainingId trainingId,
        TrainerId trainerId,
        TrainingTitle title,
        TrainingDescription description,
        TrainingPrerequisites prerequisites,
        AcquiredSkills acquiredSkills,
        IReadOnlyCollection<Topic> topics,
        IUniquenessTitleChecker titleChecker,
        ITrainingCounter trainingCounter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trainerId);
        ArgumentNullException.ThrowIfNull(trainingCounter);

        // Asked before the content is even looked at: no title makes an eleventh training
        // acceptable, so a full catalogue refuses the creation whole rather than per field.
        // Creation-only on purpose — editing changes a training, never how many there are.
        var published = await trainingCounter.CountForTrainerAsync(trainerId, cancellationToken);
        if (published >= MaximumPerTrainer)
        {
            return Result<Training>.Failure(TrainingErrorCodes.CatalogueFull,
                $"A trainer cannot publish more than {MaximumPerTrainer} trainings.");
        }

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

    /// <summary>
    /// Whether this training answers to the given trainer.
    /// </summary>
    /// <remarks>
    /// The rule itself lives in <see cref="Specifications.TrainingOwnedBySpecification"/>; this
    /// method is the aggregate wearing it, so a use case asks the object it holds rather than
    /// instantiating machinery. Kept a question on purpose — the decision of what refusing means
    /// (a 404 rather than a 403, here) belongs to the caller, not to the aggregate.
    /// </remarks>
    /// <param name="trainerId">The trainer asking.</param>
    /// <returns><see langword="true"/> when that trainer published this training.</returns>
    public bool IsOwnedBy(TrainerId trainerId)
    {
        ArgumentNullException.ThrowIfNull(trainerId);

        return new Specifications.TrainingOwnedBySpecification(trainerId).IsSatisfiedBy(this);
    }

    /// <summary>
    /// Edit this training.
    /// </summary>
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
