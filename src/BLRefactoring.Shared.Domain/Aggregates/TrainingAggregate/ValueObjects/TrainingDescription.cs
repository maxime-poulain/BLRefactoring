using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

/// <summary>
/// Represents the description of a training.
/// The description provides detailed information about the training content,
/// objectives, and what participants can expect.
/// </summary>
/// <remarks>
/// The description is limited to 500 characters to ensure conciseness
/// while providing enough detail for potential participants.
/// </remarks>
public sealed class TrainingDescription : ValueObject
{
    private const int MaxLength = 500;

    /// <summary>
    /// Gets the description text.
    /// </summary>
    public string Value { get; private init; } = null!;

    private TrainingDescription() { } // For ORM

    private TrainingDescription(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new instance of <see cref="TrainingDescription"/>.
    /// </summary>
    /// <param name="description">The description text.</param>
    /// <returns>A result containing the training description or validation errors.</returns>
    public static Result<TrainingDescription> Create(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Result<TrainingDescription>.Failure(
                ErrorCode.InvalidDescription,
                "Training description cannot be empty.");
        }

        if (description.Length > MaxLength)
        {
            return Result<TrainingDescription>.Failure(
                ErrorCode.InvalidDescription,
                $"Training description cannot exceed {MaxLength} characters.");
        }

        return Result<TrainingDescription>.Success(new TrainingDescription(description.Trim()));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
