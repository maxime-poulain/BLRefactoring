namespace BLRefactoring.Shared.Common.Errors;

/// <summary>
/// The codes that belong to no aggregate in particular.
/// </summary>
/// <remarks>
/// These four are the kernel's whole error vocabulary, and they carry no owner prefix on purpose:
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

    /// <summary>
    /// The request was rejected before the aggregate was reached.
    /// </summary>
    /// <remarks>
    /// What a validator running ahead of a handler reports. It belongs to the kernel rather than to
    /// an aggregate because it is raised before any aggregate is involved — that is the whole point
    /// of validating there — so there is no owner to name.
    /// <para>
    /// Deliberately one code rather than one per rule. The message names the field, as
    /// FluentValidation writes it; inventing a code per rule would publish a vocabulary the domain
    /// does not share, when the same field checked by the domain answers with the aggregate's own
    /// code. A caller that needs to know precisely what was wrong reads the message; a caller that
    /// needs to know it was its own request reads this.
    /// </para>
    /// </remarks>
    public static readonly ErrorCode Validation = new("Validation");
}
