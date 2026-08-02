using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using EmailValidation;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

public sealed class Email : ValueObject
{
    public string FullAddress { get; } = null!;

    /// <summary>
    /// The mailbox name, before the separator.
    /// </summary>
    public string LocalPart => FullAddress[..SeparatorIndex];

    /// <summary>
    /// The domain the mailbox belongs to, after the separator.
    /// </summary>
    public string Domain => FullAddress[(SeparatorIndex + 1)..];

    /// <summary>
    /// Position of the <c>@</c> separating the local part from the domain.
    /// </summary>
    /// <remarks>
    /// The <em>last</em> <c>@</c>, not the first. A domain never contains an unquoted one, but a
    /// quoted local part may: <c>"a@b"@example.com</c> is a single mailbox whose domain is
    /// <c>example.com</c>, where splitting on the first separator reported <c>b"</c>. Slicing also
    /// spares the intermediate array <c>Split</c> allocated on every read — the returned substring
    /// is now the only allocation.
    /// No guard for a missing separator: an <see cref="Email"/> only exists through
    /// <see cref="Create"/>, which rejects anything that is not a valid address.
    /// </remarks>
    private int SeparatorIndex => FullAddress.LastIndexOf('@');

    private Email() { } // Private constructor for ORM or serialization.

    private Email(string fullAddress)
    {
        FullAddress = fullAddress;
    }

    public static Result<Email> Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<Email>.Failure(new Error(TrainerErrorCodes.InvalidEmail, "Email should not be empty."));
        }

        if (!IsValidFormat(email))
        {
            return Result<Email>.Failure(new Error(TrainerErrorCodes.InvalidEmail, "Email format is invalid."));
        }

        return Result<Email>.Success(new Email(email));
    }

    private static bool IsValidFormat(string email) => EmailValidator.Validate(email);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FullAddress;
    }
}
