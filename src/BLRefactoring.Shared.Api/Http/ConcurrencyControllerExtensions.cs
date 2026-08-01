using BLRefactoring.Shared.Api.Contracts.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.Shared.Api.Http;

/// <summary>
/// The HTTP side of optimistic concurrency: publishing the version a caller read
/// and reading back the one they claim to have.
/// </summary>
/// <remarks>
/// Shared by both stacks so the two of them cannot drift on status codes, which is
/// where this kind of plumbing usually diverges.
/// </remarks>
public static class ConcurrencyControllerExtensions
{
    /// <summary>
    /// Publishes the aggregate's version as the response <c>ETag</c>.
    /// </summary>
    public static void SetETag(this ControllerBase controller, byte[] rowVersion)
    {
        ArgumentNullException.ThrowIfNull(controller);

        controller.Response.Headers.ETag = EntityTag.From(rowVersion);
    }

    /// <summary>
    /// Reads the version the caller states they read, from the <c>If-Match</c> header.
    /// </summary>
    public static bool TryGetExpectedVersion(this ControllerBase controller, out byte[] expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(controller);

        return EntityTag.TryParse(controller.Request.Headers.IfMatch.FirstOrDefault(), out expectedVersion);
    }

    /// <summary>
    /// 428, for a write that did not say which version it is replacing.
    /// </summary>
    /// <remarks>
    /// Refusing an unconditional write is the point: accepting it would let the
    /// caller overwrite changes they never saw.
    /// </remarks>
    public static ActionResult PreconditionRequired(this ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        return controller.StatusCode(
            StatusCodes.Status428PreconditionRequired,
            new[]
            {
                new ErrorResponseHttp
                {
                    ErrorMessage =
                        "This request must carry an If-Match header holding the ETag returned when the resource was read.",
                    ErrorCode = new ErrorCodeResponseHttp
                    {
                        Name = Common.Errors.ErrorCode.ConcurrencyConflict.Name,
                        Value = Common.Errors.ErrorCode.ConcurrencyConflict.Value
                    }
                }
            });
    }

    /// <summary>
    /// 412, for a write whose <c>If-Match</c> no longer matches the stored version.
    /// </summary>
    /// <remarks>
    /// Takes the published error contract rather than the kernel's collection: this helper writes
    /// a response body, and response bodies are the API's vocabulary. Callers map with
    /// <c>ToHttp()</c> before handing the errors over.
    /// </remarks>
    public static ActionResult PreconditionFailed(
        this ControllerBase controller,
        IEnumerable<ErrorResponseHttp> errors)
    {
        ArgumentNullException.ThrowIfNull(controller);

        return controller.StatusCode(StatusCodes.Status412PreconditionFailed, errors);
    }
}
