using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate.ValueObjects;

public sealed class Name : ValueObject
{
    public string Firstname { get; }
    public string Lastname { get; }

    private Name(string firstname, string lastname)
    {
        Firstname = firstname;
        Lastname = lastname;
    }

    public static Result<Name> Create(string firstname, string lastname)
    {
        var errors = new ErrorCollection();
        if (firstname is not { Length: >= 2 })
        {
            errors.Add(ErrorCode.Unspecified, "Firstname must be five characters long at least");
        }

        if (lastname is not { Length: >= 2 })
        {
            errors.Add(ErrorCode.Unspecified, "Lastname must be five characters long at least");
        }

        if (errors.Any())
        {
            return Result<Name>.Failure(errors);
        }

        return Result<Name>.Success(new Name(firstname, lastname));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Firstname;
        yield return Lastname;
    }
}