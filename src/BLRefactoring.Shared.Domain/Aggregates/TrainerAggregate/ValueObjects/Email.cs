using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using EmailValidation;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

public class Email : ValueObject
{
    public string FullAddress { get; } = null!;
    public string LocalPart => FullAddress.Split('@')[0];
    public string Domain => FullAddress.Split('@')[1];

    private Email() { } // Private constructor for ORM or serialization.

    private Email(string fullAddress)
    {
        FullAddress = fullAddress;
    }

    public static Result<Email> Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<Email>.Failure(new Error(ErrorCode.InvalidEmail, "Email should not be empty."));
        }

        if (!IsValidFormat(email))
        {
            return Result<Email>.Failure(new Error(ErrorCode.InvalidEmail, "Email format is invalid."));
        }

        return Result<Email>.Success(new Email(email));
    }

    private static bool IsValidFormat(string email) => EmailValidator.Validate(email);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FullAddress;
    }
}
