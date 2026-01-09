
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

/// <summary>
/// Represents the title of a training.
/// The title is a unique identifier for the training within a trainer's portfolio
/// and must be descriptive yet concise.
/// </summary>
/// <remarks>
/// The title must be between 5 and 30 characters to ensure it's both
/// meaningful and brief enough for display purposes.
/// </remarks>
public sealed class TrainingTitle : ValueObject
{
    private const int MinLength = 5;
    private const int MaxLength = 30;

    /// <summary>
    /// Gets the title text.
    /// </summary>
    public string Value { get; init; } = null!;

    private TrainingTitle() { } // For ORM

    private TrainingTitle(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new instance of <see cref="TrainingTitle"/>.
    /// </summary>
    /// <param name="title">The title text.</param>
    /// <returns>A result containing the training title or validation errors.</returns>
    public static Result<TrainingTitle> Create(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result<TrainingTitle>.Failure(ErrorCode.InvalidTitle, "Training title cannot be empty.");
        }

        var trimmedTitle = title.Trim();

        if (trimmedTitle.Length is < MinLength or > MaxLength)
        {
            return Result<TrainingTitle>.Failure(
                ErrorCode.InvalidTitle,
                $"Training title must be between {MinLength} and {MaxLength} characters.");
        }

        return Result<TrainingTitle>.Success(new TrainingTitle(trimmedTitle));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value.ToLowerInvariant();
    }

    public override string ToString() => Value;
}
