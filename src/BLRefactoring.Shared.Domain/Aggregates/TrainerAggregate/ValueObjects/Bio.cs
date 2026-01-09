using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

public class Bio : ValueObject
{
    private Bio()
    {
    }

    private Bio(string value)
    {
        Value = value;
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

        return Result<Bio>.Success(new Bio(value));
    }

    public string Value { get; } = null!;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}