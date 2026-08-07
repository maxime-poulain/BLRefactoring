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
    /// Whether this training is offered to the public or withdrawn from it.
    /// </summary>
    /// <remarks>
    /// A training is born <see cref="TrainingStatus.Published"/>, which the initialiser states
    /// rather than the factory: there is no path that produces a training in any other state, so
    /// the default belongs to the field. It is only ever moved by <see cref="PublishAsync"/> and
    /// <see cref="Unpublish"/>, each of which announces the move.
    /// <para>
    /// This is half of what makes a training publicly visible. The other half is its owner's
    /// standing, and the pair is composed at the point of asking rather than stored here — a
    /// suspension writes one field on one aggregate and touches no training at all (ADR 0050).
    /// </para>
    /// </remarks>
    public TrainingStatus Status { get; private set; } = TrainingStatus.Published;

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
        ITrainerStanding trainerStanding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trainerId);
        ArgumentNullException.ThrowIfNull(trainingCounter);
        ArgumentNullException.ThrowIfNull(trainerStanding);

        // Standing before capacity: a suspended trainer may not add to what the public can see,
        // and that refusal does not depend on how full their catalogue is (ADR 0050).
        if (await trainerStanding.IsSuspendedAsync(trainerId, cancellationToken))
        {
            return Result<Training>.Failure(TrainingErrorCodes.TrainerSuspended,
                "A suspended trainer cannot publish a new training.");
        }

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
    /// Offers this training to the public again, when its owner's standing and catalogue allow it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The capacity question is asked here and not only at creation, and that is the whole reason
    /// this method takes a counter. Once the quota counts published trainings alone, a trainer
    /// sitting at the limit could unpublish one, create a replacement, and republish the first —
    /// eleven trainings on offer, each one added through a check that passed. Publishing is the
    /// second act that grows the public catalogue, so it answers the same rule creation does.
    /// </para>
    /// <para>
    /// Editing takes no such argument and never will: an edition changes a training, never how
    /// many of them the public can see.
    /// </para>
    /// </remarks>
    /// <param name="trainerStanding">Answers whether the owner is under sanction.</param>
    /// <param name="trainingCounter">Answers how many trainings the owner already publishes.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>Success, or the rule that refused.</returns>
    public async Task<Result> PublishAsync(
        ITrainerStanding trainerStanding,
        ITrainingCounter trainingCounter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trainerStanding);
        ArgumentNullException.ThrowIfNull(trainingCounter);

        // Asked first, like the transfer asks whether the recipient is already the owner: a move
        // to the state the aggregate is already in makes the rules below vacuous, and answering
        // it with success would put a fact on the wire that nothing happened for.
        if (Status == TrainingStatus.Published)
        {
            return Result.Failure(TrainingErrorCodes.AlreadyPublished,
                "This training is already published.");
        }

        if (await trainerStanding.IsSuspendedAsync(TrainerId, cancellationToken))
        {
            return Result.Failure(TrainingErrorCodes.TrainerSuspended,
                "A suspended trainer cannot publish a training.");
        }

        var published = await trainingCounter.CountForTrainerAsync(TrainerId, cancellationToken);
        if (published >= MaximumPerTrainer)
        {
            return Result.Failure(TrainingErrorCodes.CatalogueFull,
                $"A trainer cannot publish more than {MaximumPerTrainer} trainings.");
        }

        Status = TrainingStatus.Published;
        AddDomainEvent(new TrainingPublishedDomainEvent(Id, TrainerId));

        return Result.Success();
    }

    /// <summary>
    /// Withdraws this training from public view.
    /// </summary>
    /// <remarks>
    /// Takes no port, and that is a decision rather than an omission: withdrawing shrinks what the
    /// public sees, so no rule about a trainer's standing or capacity can stand in its way. A
    /// suspended trainer may unpublish, which is part of leaving them able to repair what earned
    /// them the sanction (ADR 0050). The training keeps its title and its rows; what it gives up is
    /// its place in the quota and its entry in the search index.
    /// </remarks>
    /// <returns>Success, or a refusal when the training was already withdrawn.</returns>
    public Result Unpublish()
    {
        if (Status == TrainingStatus.Unpublished)
        {
            return Result.Failure(TrainingErrorCodes.AlreadyUnpublished,
                "This training is already unpublished.");
        }

        Status = TrainingStatus.Unpublished;
        AddDomainEvent(new TrainingUnpublishedDomainEvent(Id, TrainerId));

        return Result.Success();
    }

    /// <summary>
    /// Marks this training for deletion, announcing the fact so that what was built from it — its
    /// entry in the search index, first of all — can be dealt with.
    /// </summary>
    /// <remarks>
    /// Deleting a training used to raise nothing at all, which is why an indexed training outlived
    /// its own rows in the index for ever. The method mirrors <c>Trainer.MarkForDeletion</c>: the
    /// aggregate states the fact, and removing the rows stays the repository's act in the same unit
    /// of work.
    /// <para>
    /// Deletion survives <see cref="Unpublish"/> rather than being replaced by it, and answers a
    /// different need: the training created by mistake, and the trainer exercising a right to have
    /// their data removed — which a system that only ever hides things cannot honour.
    /// </para>
    /// </remarks>
    public void MarkForDeletion()
    {
        AddDomainEvent(new TrainingDeletedDomainEvent(Id, TrainerId));
    }

    /// <summary>
    /// Hands this training to a new owner: raises the fact — both owners on it — then reassigns.
    /// </summary>
    /// <remarks>
    /// Internal on purpose (ADR 0036): the only public path to reassignment is
    /// <see cref="TrainingTransferDomainService"/>, whose signature demands the recipient-side facts,
    /// so a transfer that skipped the capacity and title questions does not compile outside the
    /// domain assembly — the same compile-time answer creation gives through its factory.
    /// </remarks>
    /// <param name="newOwner">The trainer receiving the training.</param>
    internal void TransferTo(TrainerId newOwner)
    {
        ArgumentNullException.ThrowIfNull(newOwner);

        // Raised before the assignment: the event needs the former owner, and the aggregate is
        // about to forget them.
        AddDomainEvent(new TrainingTransferredDomainEvent(Id, TrainerId, newOwner));
        TrainerId = newOwner;
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
