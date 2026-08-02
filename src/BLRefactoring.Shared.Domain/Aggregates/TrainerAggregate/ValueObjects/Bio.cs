using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

/// <summary>
/// The free-text presentation a trainer writes about themselves.
/// </summary>
/// <remarks>
/// How this is stored is none of the domain's business: <c>TrainerConfiguration</c> maps it as an
/// owned type, in the infrastructure. This class used to carry EF Core's <c>[Owned]</c> attribute
/// as well — redundant with that mapping, and the single reason the domain project referenced a
/// persistence package at all.
/// </remarks>
public sealed class Bio : ValueObject
{
    // private init, like every other value object here. A public init is a public setter as far as
    // reflection is concerned: it is an ordinary set accessor wearing a required modifier, and
    // anything holding a Bio could have built a different one with `with`-style initialisation.
    // Create still reaches it — private means private to the type, and the object initialiser below
    // is inside the type.

    /// <summary>
    /// The text this value object wraps.
    /// </summary>
    public string Value { get; private init; } = null!;

    private Bio()
    {
    }

    /// <summary>
    /// Builds a <see cref="Bio"/> from raw input.
    /// </summary>
    /// <returns>
    /// The value, or every rule it broke. Failure is returned rather than thrown: a
    /// caller sending three bad fields learns about all three at once.
    /// </returns>
    public static Result<Bio> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<Bio>.Failure(TrainerErrorCodes.BioEmpty, "Bio cannot be empty.");
        }

        if (value.Length > 500)
        {
            return Result<Bio>.Failure(TrainerErrorCodes.BioExceeds500Characters, "Bio cannot exceed 500 characters.");
        }

        return Result<Bio>.Success(new Bio() { Value = value });
    }

    /// <summary>
    /// Yields the parts this value is compared by.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
