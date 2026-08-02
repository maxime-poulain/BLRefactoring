namespace BLRefactoring.Shared.Common.Errors;

/// <summary>
/// The codes that belong to no aggregate in particular.
/// </summary>
/// <remarks>
/// These three are the kernel's whole error vocabulary, and they carry no owner prefix on purpose:
/// "not found" and "concurrency conflict" are true of any aggregate, so naming one would be a lie.
/// Everything specific to a trainer or a training is declared where that aggregate lives.
/// </remarks>
public static class ErrorCodes
{
    /// <summary>No more precise code was available. Nothing should be answering with this one.</summary>
    public static readonly ErrorCode Unspecified = new("Unspecified");

    /// <summary>The aggregate the caller named does not exist.</summary>
    public static readonly ErrorCode NotFound = new("NotFound");

    /// <summary>The aggregate was modified by someone else since the caller read it.</summary>
    public static readonly ErrorCode ConcurrencyConflict = new("ConcurrencyConflict");
}
