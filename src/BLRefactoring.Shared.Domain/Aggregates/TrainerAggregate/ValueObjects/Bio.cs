using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
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
    public string Value { get; init; } = null!;

    private Bio()
    {
    }

    public static Result<Bio> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<Bio>.Failure(ErrorCode.BioEmpty, "Bio cannot be empty.");
        }

        if (value.Length > 500)
        {
            return Result<Bio>.Failure(ErrorCode.BioExceeds500Characters, "Bio cannot exceed 500 characters.");
        }

        return Result<Bio>.Success(new Bio() { Value = value });
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
