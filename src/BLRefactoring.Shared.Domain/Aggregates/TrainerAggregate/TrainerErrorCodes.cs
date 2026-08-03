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

    /// <summary>An upload arrived with no bytes in it.</summary>
    public static readonly ErrorCode PhotoEmpty = new("Trainer.PhotoEmpty");

    /// <summary>The photo is larger than the aggregate accepts.</summary>
    public static readonly ErrorCode PhotoTooLarge = new("Trainer.PhotoTooLarge");

    /// <summary>The photo is not one of the image formats this domain publishes.</summary>
    public static readonly ErrorCode PhotoFormatNotSupported = new("Trainer.PhotoFormatNotSupported");

    /// <summary>
    /// The bytes are a supported image, but not the one the caller said they were sending.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="PhotoFormatNotSupported"/> on purpose: this one tells a caller
    /// their upload is fine and their <c>Content-Type</c> is wrong, which is a different fix.
    /// </remarks>
    public static readonly ErrorCode PhotoContentMismatch = new("Trainer.PhotoContentMismatch");
}
