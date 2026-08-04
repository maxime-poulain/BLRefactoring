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
}
