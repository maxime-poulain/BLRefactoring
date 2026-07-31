namespace BLRefactoring.Shared.Api.Contracts.Errors;

/// <summary>
/// One problem reported to a caller, as the API describes it.
/// </summary>
/// <remarks>
/// The API's own type rather than the shared kernel's <c>Error</c>. The kernel's version is a
/// value object carrying an <c>ErrorCode</c> smart enum: internal vocabulary, free to gain a
/// member or rename one, and until now every such change was a change to the public contract
/// without anyone deciding it was.
/// <para>
/// Its shape mirrors what the kernel used to serialise, down to the nested code, so this
/// refactoring changes no payload. Swapping it for <c>ProblemDetails</c> later is then a change
/// to this file and its mapper, and to nothing else.
/// </para>
/// </remarks>
public sealed class ErrorResponseHttp
{
    /// <summary>
    /// What went wrong, in terms a caller can act on.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// The machine-readable code a client branches on.
    /// </summary>
    public required ErrorCodeResponseHttp ErrorCode { get; init; }
}

/// <summary>
/// The published form of an error code: a stable name and its numeric value.
/// </summary>
public sealed class ErrorCodeResponseHttp
{
    /// <summary>
    /// The code's name, for example <c>DuplicateTitle</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The code's numeric value.
    /// </summary>
    public required int Value { get; init; }
}
