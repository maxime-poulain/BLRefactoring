using TrainingHub.Shared.Common.Errors;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

/// <summary>
/// Everything that can go wrong with a training, named by the aggregate that owns the rule.
/// </summary>
/// <remarks>
/// These used to be declared in the shared kernel, next to <see cref="ErrorCode"/> itself, which put
/// the words "duplicate title" in a project whose whole purpose is to know no business. They live
/// here now, beside the invariants that raise them.
/// </remarks>
public static class TrainingErrorCodes
{
    /// <summary>The title is empty, or longer than the aggregate allows.</summary>
    public static readonly ErrorCode InvalidTitle = new("Training.InvalidTitle");

    /// <summary>This trainer already has a training under that title.</summary>
    public static readonly ErrorCode DuplicateTitle = new("Training.DuplicateTitle");

    /// <summary>This trainer already publishes as many trainings as the catalogue allows.</summary>
    public static readonly ErrorCode CatalogueFull = new("Training.CatalogueFull");

    /// <summary>The description is empty, or longer than the aggregate allows.</summary>
    public static readonly ErrorCode InvalidDescription = new("Training.InvalidDescription");

    /// <summary>The prerequisites are empty, or longer than the aggregate allows.</summary>
    public static readonly ErrorCode InvalidPrerequisites = new("Training.InvalidPrerequisites");

    /// <summary>The acquired skills are empty, or longer than the aggregate allows.</summary>
    public static readonly ErrorCode InvalidAcquiredSkills = new("Training.InvalidAcquiredSkills");

    /// <summary>The topic named by the caller is not one of the six the domain knows.</summary>
    public static readonly ErrorCode InvalidTopic = new("Training.InvalidTopic");

    /// <summary>The transfer names the current owner as the recipient (ADR 0036).</summary>
    public static readonly ErrorCode TransferToSelf = new("Training.TransferToSelf");

    /// <summary>
    /// The recipient already publishes as many trainings as the catalogue allows. Distinct from
    /// <see cref="CatalogueFull"/> on purpose: "delete one of yours" and "pick another colleague"
    /// are different instructions to a caller (ADR 0015, ADR 0036).
    /// </summary>
    public static readonly ErrorCode RecipientCatalogueFull = new("Training.RecipientCatalogueFull");

    /// <summary>The transfer names a recipient no trainer answers to (ADR 0036).</summary>
    public static readonly ErrorCode UnknownRecipient = new("Training.UnknownRecipient");

    /// <summary>The training is already offered to the public, so publishing it changes nothing.</summary>
    public static readonly ErrorCode AlreadyPublished = new("Training.AlreadyPublished");

    /// <summary>The training is already withdrawn, so unpublishing it changes nothing.</summary>
    public static readonly ErrorCode AlreadyUnpublished = new("Training.AlreadyUnpublished");

    /// <summary>
    /// The trainer is under sanction and may not grow what the public can see (ADR 0050).
    /// </summary>
    public static readonly ErrorCode TrainerSuspended = new("Training.TrainerSuspended");

    /// <summary>
    /// The recipient of a transfer is under sanction. Distinct from <see cref="TrainerSuspended"/>
    /// for the reason <see cref="RecipientCatalogueFull"/> is distinct from
    /// <see cref="CatalogueFull"/>: "your account is suspended" and "theirs is" send a caller to
    /// different places (ADR 0015).
    /// </summary>
    public static readonly ErrorCode RecipientSuspended = new("Training.RecipientSuspended");
}
