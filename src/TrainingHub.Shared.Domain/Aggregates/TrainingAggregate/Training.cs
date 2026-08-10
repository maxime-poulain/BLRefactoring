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
/// read-only, and every transition goes through a behavior method that either succeeds entirely
/// or changes nothing. It accepts value objects, never a raw <see langword="string"/> — turning
/// input into those is the application layer's job.
/// </para>
/// </summary>
public sealed class Training : AggregateRoot<TrainingId>
{
    /// <summary>
    /// How many trainings one trainer may publish: no trainer's catalog holds more than ten.
    /// </summary>
    /// <remarks>
    /// A business rule, not a technical bound — the number is the domain expert's, and it lives
    /// here because the rule is about trainings even though it is counted per trainer, exactly
    /// as title uniqueness is. <see cref="CreateAsync"/> enforces it; nothing else reads it as
    /// permission to exist, so a catalog that was over the limit before the rule existed keeps
    /// its trainings and merely cannot grow.
    /// </remarks>
    public const int MaximumPerTrainer = 10;

    private readonly List<Topic> _topics = [];

    /// <summary>
    /// The topics this training is filed under. Read-only: a caller changes them through a
    /// behavior method or not at all.
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
    /// A training is born <see cref="TrainingStatus.Published"/>, which the initializer states
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
    /// Why the administration withheld this training, or <see langword="null"/> when it did not.
    /// </summary>
    /// <remarks>
    /// The invariant is stated in both directions: the reason is present <em>if and only if</em>
    /// <see cref="Status"/> is <see cref="TrainingStatus.Withheld"/>. That is what forbids an orphan
    /// reason and a mute state alike, and it holds by construction — <see cref="Withhold"/> and
    /// <see cref="Release"/> are the only writers, and each moves both fields together (ADR 0052).
    /// <para>
    /// It lives here rather than only on the fact that announced it, because the owner has to be
    /// able to read it afterwards and the outbox is swept (ADR 0033).
    /// </para>
    /// </remarks>
    public WithholdingReason? WithholdingReason { get; private set; }

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
        // and that refusal does not depend on how full their catalog is (ADR 0050).
        if (await trainerStanding.IsSuspendedAsync(trainerId, cancellationToken))
        {
            return Result<Training>.Failure(TrainingErrorCodes.TrainerSuspended,
                "A suspended trainer cannot publish a new training.");
        }

        // Asked before the content is even looked at: no title makes an eleventh training
        // acceptable, so a full catalog refuses the creation whole rather than per field.
        // Creation-only on purpose — editing changes a training, never how many there are.
        var published = await trainingCounter.CountForTrainerAsync(trainerId, cancellationToken);
        if (published >= MaximumPerTrainer)
        {
            return Result<Training>.Failure(TrainingErrorCodes.CatalogFull,
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
    /// Offers this training to the public again, when its owner's standing and catalog allow it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The capacity question is asked here and not only at creation, and that is the whole reason
    /// this method takes a counter. Once the quota counts published trainings alone, a trainer
    /// sitting at the limit could unpublish one, create a replacement, and republish the first —
    /// eleven trainings on offer, each one added through a check that passed. Publishing is the
    /// second act that grows the public catalog, so it answers the same rule creation does.
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

        // The refusal the third state exists to produce, and the reason setting Unpublished would
        // have been worth nothing: this endpoint is the owner's, so an administration that only
        // withdrew a training would be undone by one request (ADR 0052).
        if (Status == TrainingStatus.Withheld)
        {
            return Result.Failure(TrainingErrorCodes.Withheld,
                "This training has been withheld and its owner cannot publish it.");
        }

        if (await trainerStanding.IsSuspendedAsync(TrainerId, cancellationToken))
        {
            return Result.Failure(TrainingErrorCodes.TrainerSuspended,
                "A suspended trainer cannot publish a training.");
        }

        var published = await trainingCounter.CountForTrainerAsync(TrainerId, cancellationToken);
        if (published >= MaximumPerTrainer)
        {
            return Result.Failure(TrainingErrorCodes.CatalogFull,
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
    /// Takes no port, and that is still a decision rather than an omission — but no longer the same
    /// one. Withdrawing shrinks what the public sees, so no rule about capacity can stand in its
    /// way; a suspended trainer may not unpublish either, and that refusal is the boundary's rather
    /// than this aggregate's (ADR 0053). The signature stays empty so the domain does not grow a
    /// rule it would answer for one caller twice. The training keeps its title and its rows; what it
    /// gives up is its place in the quota and its entry in the search index.
    /// </remarks>
    /// <returns>Success, or a refusal when the training was already withdrawn.</returns>
    public Result Unpublish()
    {
        if (Status == TrainingStatus.Unpublished)
        {
            return Result.Failure(TrainingErrorCodes.AlreadyUnpublished,
                "This training is already unpublished.");
        }

        // The owner cannot leave Withheld by any door, and this is the second one. Refusing only in
        // PublishAsync would leave the interdiction liftable in two moves — unpublish, then publish
        // — each of them through a check that passed, which is the shape of the bypass ADR 0050
        // found in the quota.
        if (Status == TrainingStatus.Withheld)
        {
            return Result.Failure(TrainingErrorCodes.Withheld,
                "This training has been withheld and its owner cannot change that.");
        }

        Status = TrainingStatus.Unpublished;
        AddDomainEvent(new TrainingUnpublishedDomainEvent(Id, TrainerId));

        return Result.Success();
    }

    /// <summary>
    /// Takes this training out of public view by administrative decision, with a reason its owner
    /// can read.
    /// </summary>
    /// <remarks>
    /// Accepts either starting state, and that is deliberate: prohibited content is prohibited
    /// whether or not it is currently on offer, and withholding an already-withdrawn training is
    /// precisely what stops its owner putting it back.
    /// <para>
    /// The training keeps its place in the quota. ADR 0050 made the ten count what the public can
    /// see, on the grounds that a trainer who withdraws ten should not lose their catalog for
    /// ever — but that argument is about a <em>voluntary</em> withdrawal, and freeing a slot by
    /// being moderated is a perverse incentive. See
    /// <see cref="Specifications.TrainingCountsTowardTheQuotaSpecification"/>.
    /// </para>
    /// <para>
    /// Who may call this is not the domain's business, exactly as <see cref="IsOwnedBy"/> answers a
    /// question and leaves the caller to decide what refusing means. The role lives at the API
    /// (ADR 0051).
    /// </para>
    /// </remarks>
    /// <param name="reason">Why the training is being withheld.</param>
    /// <returns>Success, or a refusal when the training was already withheld.</returns>
    public Result Withhold(WithholdingReason reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        if (Status == TrainingStatus.Withheld)
        {
            return Result.Failure(TrainingErrorCodes.AlreadyWithheld,
                "This training is already withheld.");
        }

        Status = TrainingStatus.Withheld;
        WithholdingReason = reason;
        AddDomainEvent(new TrainingWithheldDomainEvent(Id, TrainerId, Title, reason));

        return Result.Success();
    }

    /// <summary>
    /// Lifts the interdiction, leaving the training withdrawn rather than on offer.
    /// </summary>
    /// <remarks>
    /// It lands on <see cref="TrainingStatus.Unpublished"/> and never on
    /// <see cref="TrainingStatus.Published"/>, and no memory of the state before the withholding is
    /// kept. The administration lifts an interdiction; it does not decide to put a training back in
    /// the window. Restoring the prior state would need a second field, written on every
    /// withholding and read once — the kind of field ADR 0050 refused when the behavior can be had
    /// without it (ADR 0052).
    /// </remarks>
    /// <returns>Success, or a refusal when there was nothing to lift.</returns>
    public Result Release()
    {
        if (Status != TrainingStatus.Withheld)
        {
            return Result.Failure(TrainingErrorCodes.NotWithheld,
                "This training is not withheld, so there is nothing to release.");
        }

        Status = TrainingStatus.Unpublished;
        WithholdingReason = null;
        AddDomainEvent(new TrainingReleasedDomainEvent(Id, TrainerId));

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
    /// their data removed — which a system that only ever hides things cannot honor.
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
