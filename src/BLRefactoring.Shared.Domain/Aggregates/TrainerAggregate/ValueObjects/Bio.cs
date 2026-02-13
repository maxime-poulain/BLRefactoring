using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

[Owned]
public class Bio : ValueObject
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
