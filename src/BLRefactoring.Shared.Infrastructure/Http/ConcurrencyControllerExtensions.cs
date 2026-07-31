using BLRefactoring.Shared.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.Shared.Infrastructure.Http;

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
                new Error(
                    ErrorCode.ConcurrencyConflict,
                    "This request must carry an If-Match header holding the ETag returned when the resource was read.")
            });
    }

    /// <summary>
    /// 412, for a write whose <c>If-Match</c> no longer matches the stored version.
    /// </summary>
    public static ActionResult PreconditionFailed(this ControllerBase controller, IReadOnlyErrorCollection errors)
    {
        ArgumentNullException.ThrowIfNull(controller);

        return controller.StatusCode(StatusCodes.Status412PreconditionFailed, errors);
    }
}
