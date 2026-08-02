using BLRefactoring.Shared.Common.Errors;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

/// <summary>
/// Everything that can go wrong with a trainer, named by the aggregate that owns the rule.
/// </summary>
public static class TrainerErrorCodes
{
    /// <summary>The address is not one this domain accepts.</summary>
    public static readonly ErrorCode InvalidEmail = new("Trainer.InvalidEmail");

    /// <summary>A bio was supplied, and it is blank.</summary>
    public static readonly ErrorCode BioEmpty = new("Trainer.BioEmpty");

    /// <summary>The bio is longer than the five hundred characters the aggregate allows.</summary>
    public static readonly ErrorCode BioExceeds500Characters = new("Trainer.BioExceeds500Characters");
}
