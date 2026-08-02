namespace BLRefactoring.Shared.Api.Contracts.Errors;

/// <summary>
/// One problem reported to a caller, as the API describes it.
/// </summary>
/// <remarks>
/// The API's own type rather than the shared kernel's <c>Error</c>. The kernel's version is a value
/// object carrying an <c>ErrorCode</c>: internal vocabulary, free to gain a member or rename one,
/// and until this type existed every such change was a change to the public contract without anyone
/// deciding it was.
/// <para>
/// The code used to be published as a nested object — a name and a number — because that is what the
/// kernel's smart enum happened to hold. Nothing ever read the number: not the generated client,
/// which does not model this type at all now that errors travel as a <c>domainErrors</c> extension
/// on a problem document, not the front end, and not the one test that reads a code. It was a field
/// maintained for nobody, and every new code had to be given a number to keep it fed. The code is
/// now the string it always was.
/// </para>
/// </remarks>
public sealed class ErrorResponseHttp
{
    /// <summary>
    /// What went wrong, in terms a caller can act on.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// The machine-readable code a client branches on, for example <c>Training.DuplicateTitle</c>.
    /// </summary>
    public required string ErrorCode { get; init; }
}
