using Ardalis.SmartEnum;

namespace BLRefactoring.Shared.Common.Errors;

/// <summary>
/// Represents the base class that each error code should inherit from.
/// </summary>
public class ErrorCode : SmartEnum<ErrorCode>
{
    public static readonly ErrorCode Unspecified = new("Unspecified", -1);
    public static readonly ErrorCode NotFound = new("NotFound", -2);

    protected ErrorCode(string name, int value) : base(name, value)
    {
    }

    // Training error codes.
    public static readonly ErrorCode InvalidTitle = new("InvalidRequest", 1);
    public static readonly ErrorCode DuplicateTitle = new("DuplicateTitle", 2);
    public static readonly ErrorCode InvalidDescription = new("InvalidDescription", 3);
    public static readonly ErrorCode InvalidPrerequisites = new("InvalidPrerequisites", 4);
    public static readonly ErrorCode InvalidAcquiredSkills = new("InvalidAcquiredSkills", 5);

    // Trainer error codes.
    public static readonly ErrorCode InvalidTrainer = new("InvalidTrainer", 100);
    public static readonly ErrorCode InvalidEmail = new("InvalidEmail", 101);
    public static readonly ErrorCode BioEmpty = new("BioEmpty", 102);
    public static readonly ErrorCode BioExceeds500Characters = new("BioExceeds500Characters", 103);
}
