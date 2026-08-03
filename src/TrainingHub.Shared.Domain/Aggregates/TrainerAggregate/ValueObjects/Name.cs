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
    /// <returns>
    /// The value, or every rule it broke. Failure is returned rather than thrown: a
    /// caller sending three bad fields learns about all three at once.
    /// </returns>
    public static Result<Name> Create(string firstname, string lastname)
    {
        var errors = new ErrorCollection();
        if (firstname is not { Length: >= 2 and <= 50 })
        {
            errors.Add(ErrorCodes.Unspecified, "Firstname must be two characters long at least");
        }

        if (lastname is not { Length: >= 2 and <= 50 })
        {
            errors.Add(ErrorCodes.Unspecified, "Lastname must be two characters long at least");
        }

        if (errors.Any())
        {
            return Result<Name>.Failure(errors);
        }

        return Result<Name>.Success(new Name(firstname, lastname));
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
