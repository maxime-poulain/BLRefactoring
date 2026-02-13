using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

/// <summary>
/// Represents the prerequisites required to attend a training.
/// Prerequisites describe the knowledge, skills, or conditions that
/// participants must have before enrolling in the training.
/// </summary>
/// <remarks>
/// The prerequisites are limited to 500 characters to ensure clarity
/// and help participants quickly assess if they meet the requirements.
/// </remarks>
public sealed class TrainingPrerequisites : ValueObject
{
    private const int MaxLength = 500;

    /// <summary>
    /// Gets the prerequisites text.
    /// </summary>
    public string Value { get; private init; } = null!;

    private TrainingPrerequisites() { } // For ORM

    private TrainingPrerequisites(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new instance of <see cref="TrainingPrerequisites"/>.
    /// </summary>
    /// <param name="prerequisites">The prerequisites text.</param>
    /// <returns>A result containing the training prerequisites or validation errors.</returns>
    public static Result<TrainingPrerequisites> Create(string prerequisites)
    {
        if (string.IsNullOrWhiteSpace(prerequisites))
        {
            return Result<TrainingPrerequisites>.Failure(
                ErrorCode.InvalidPrerequisites, 
                "Training prerequisites cannot be empty.");
        }

        if (prerequisites.Length > MaxLength)
        {
            return Result<TrainingPrerequisites>.Failure(
                ErrorCode.InvalidPrerequisites, 
                $"Training prerequisites cannot exceed {MaxLength} characters.");
        }

        return Result<TrainingPrerequisites>.Success(new TrainingPrerequisites(prerequisites.Trim()));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
