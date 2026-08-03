using TrainingHub.Shared.Api.Contracts.Errors;
using TrainingHub.Shared.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.Shared.Api.Http;

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

    // There used to be a TryGetExpectedVersion here, reading If-Match straight off
    // Request.Headers. That is exactly why no generated client could send one: a header nobody
    // declares never reaches the OpenAPI document. The actions now bind it as a parameter and call
    // EntityTag.TryParse themselves. See ADR 0010.
    //
    // One behavioural nuance came with the move. The old code took the first value of a
    // multi-valued If-Match; a bound string receives them joined by commas. Both fail TryParse and
    // come out as 428 — same answer, reached by a different route.

    /// <summary>
    /// 428, for a write that did not say which version it is replacing.
    /// </summary>
    /// <remarks>
    /// Refusing an unconditional write is the point: accepting it would let the
    /// caller overwrite changes they never saw.
    /// <para>
    /// The body goes through <see cref="ProblemResultExtensions.Problem(ControllerBase, int, IReadOnlyList{ErrorResponseHttp})"/>
    /// like every other failure. This one has no <c>Result</c> behind it — the application layer
    /// never hears about a missing header — so it states its own error, and still comes out in the
    /// same shape.
    /// </para>
    /// </remarks>
    public static ActionResult PreconditionRequired(this ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        return controller.Problem(
            StatusCodes.Status428PreconditionRequired,
            new[]
            {
                new ErrorResponseHttp
                {
                    ErrorMessage =
                        "This request must carry an If-Match header holding the ETag returned when the resource was read.",
                    ErrorCode = ErrorCodes.ConcurrencyConflict.Value
                }
            });
    }
}
