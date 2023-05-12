using Ardalis.SmartEnum;

namespace BLRefactoring.Shared.Common.Errors;

/// <summary>
/// Represents the base class that each error code should inherit from.
/// </summary>
public class ErrorCode : SmartEnum<ErrorCode>
{
    public static readonly ErrorCode Unspecified = new("Unspecified", -1);

    protected ErrorCode(string name, int value) : base(name, value)
    {
    }

    // Training error codes.
    public static readonly ErrorCode InvalidTitle = new("InvalidRequest", 1);
    public static readonly ErrorCode DuplicateTitle = new("DuplicateTitle", 2);
    public static readonly ErrorCode InvalidStartDate = new("InvalidStartDate", 3);
    public static readonly ErrorCode InvalidEndDate = new("InvalidEndDate", 4);
    public static readonly ErrorCode InvalidRates = new("InvalidRates", 5);

    // Trainer error codes.
    public static readonly ErrorCode InvalidTrainer = new("InvalidTrainer", 100);
    public static readonly ErrorCode InvalidEmail = new("InvalidEmail", 101);
}
