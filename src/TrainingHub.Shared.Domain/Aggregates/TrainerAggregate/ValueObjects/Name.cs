using TrainingHub.Shared.Common;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

/// <summary>
/// A trainer's first and last name, held together because they are validated together.
/// </summary>
public sealed class Name : ValueObject
{
    /// <summary>
    /// The trainer's first name.
    /// </summary>
    public string Firstname { get; } = null!;

    /// <summary>
    /// The trainer's last name.
    /// </summary>
    public string Lastname { get; } = null!;

    private Name()
    {
    }

    private Name(string firstname, string lastname)
    {
        Firstname = firstname;
        Lastname = lastname;
    }

    /// <summary>
    /// Builds a <see cref="Name"/> from raw input.
    /// </summary>
    /// <remarks>
    /// Both halves are trimmed before they are measured, so <c>" Bob "</c> is the same name as
    /// <c>"Bob"</c> — surrounding whitespace is how a value was typed, not what it is (ADR 0044).
    /// The two refusals carry a code each rather than one between them, because this factory
    /// deliberately reports both fields at once and a single code would throw that away at the
    /// moment the caller most needs it.
    /// </remarks>
    /// <returns>
    /// The value, or every rule it broke. Failure is returned rather than thrown: a
    /// caller sending three bad fields learns about all three at once.
    /// </returns>
    public static Result<Name> Create(string firstname, string lastname)
    {
        var trimmedFirstname = firstname?.Trim();
        var trimmedLastname = lastname?.Trim();

        var errors = new ErrorCollection();
        if (trimmedFirstname is not { Length: >= 2 and <= 50 })
        {
            errors.Add(
                TrainerErrorCodes.InvalidFirstname,
                "Firstname must be between 2 and 50 characters long.");
        }

        if (trimmedLastname is not { Length: >= 2 and <= 50 })
        {
            errors.Add(
                TrainerErrorCodes.InvalidLastname,
                "Lastname must be between 2 and 50 characters long.");
        }

        if (errors.Any())
        {
            return Result<Name>.Failure(errors);
        }

        return Result<Name>.Success(new Name(trimmedFirstname!, trimmedLastname!));
    }

    /// <summary>
    /// Yields the parts this value is compared by.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Firstname;
        yield return Lastname;
    }
}
