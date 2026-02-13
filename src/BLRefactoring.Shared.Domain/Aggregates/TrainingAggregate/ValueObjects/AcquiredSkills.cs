using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

/// <summary>
/// Represents the skills or competencies that participants will acquire
/// upon completion of the training.
/// This describes the learning outcomes and practical abilities gained.
/// </summary>
/// <remarks>
/// The acquired skills description is limited to 500 characters to ensure
/// participants can quickly understand the benefits and outcomes of the training.
/// </remarks>
public sealed class AcquiredSkills : ValueObject
{
    private const int MaxLength = 500;

    /// <summary>
    /// Gets the acquired skills text.
    /// </summary>
    public string Value { get; private init; } = null!;

    private AcquiredSkills() { } // For ORM

    private AcquiredSkills(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new instance of <see cref="AcquiredSkills"/>.
    /// </summary>
    /// <param name="skills">The acquired skills text.</param>
    /// <returns>A result containing the acquired skills or validation errors.</returns>
    public static Result<AcquiredSkills> Create(string skills)
    {
        if (string.IsNullOrWhiteSpace(skills))
        {
            return Result<AcquiredSkills>.Failure(
                ErrorCode.InvalidAcquiredSkills, 
                "Acquired skills description cannot be empty.");
        }

        if (skills.Length > MaxLength)
        {
            return Result<AcquiredSkills>.Failure(
                ErrorCode.InvalidAcquiredSkills, 
                $"Acquired skills description cannot exceed {MaxLength} characters.");
        }

        return Result<AcquiredSkills>.Success(new AcquiredSkills(skills.Trim()));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
