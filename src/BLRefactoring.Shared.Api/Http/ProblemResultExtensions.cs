using BLRefactoring.Shared.Api.Contracts.Errors;
using BLRefactoring.Shared.Api.Contracts.Mappings;
using BLRefactoring.Shared.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.Shared.Api.Http;

/// <summary>
/// Turns a failed <c>Result</c> into the one error body this API publishes.
/// </summary>
/// <remarks>
/// Every failure leaves through here, so the shape is decided once. It used to be decided at each
/// of the forty places a controller returned one, which is how the API ended up serving four
/// different error shapes depending on where a request failed.
/// <para>
/// The body is a <see cref="ProblemDetails"/>, RFC 7807, because that is already what
/// <c>[ApiController]</c> produces on its own for a malformed body or a bare <c>NotFound()</c> —
/// so publishing anything else meant disagreeing with the framework inside the same API.
/// </para>
/// <para>
/// The domain codes are not lost to the standard: the <c>errors</c> extension carries the same
/// <see cref="ErrorResponseHttp"/> array as before, unchanged down to the nested code. A client
/// branching on <c>DuplicateTitle</c> keeps its logic and changes only where it reads it from.
/// </para>
/// </remarks>
public static class ProblemResultExtensions
{
    /// <summary>
    /// Builds the problem body for a business failure, at the status code the caller decides.
    /// </summary>
    /// <remarks>
    /// The status code stays the controller's call. Whether a duplicate title is a 409 and a
    /// version mismatch a 412 is a protocol decision, and this helper deliberately does not make
    /// it — it only guarantees that whatever the answer, the body looks the same.
    /// </remarks>
    /// <param name="controller">The controller answering the request.</param>
    /// <param name="statusCode">The status code this failure maps to.</param>
    /// <param name="errors">The failure as the application layer reported it.</param>
    public static ActionResult Problem(
        this ControllerBase controller,
        int statusCode,
        IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(errors);

        return controller.Problem(statusCode, errors.ToHttp());
    }

    /// <summary>
    /// Builds the problem body from errors already expressed in the API's own vocabulary.
    /// </summary>
    /// <remarks>
    /// The overload exists for the few places that have no <c>Result</c> to start from — a
    /// missing <c>If-Match</c> header, for one, which is a failure the application layer never
    /// hears about.
    /// </remarks>
    public static ActionResult Problem(
        this ControllerBase controller,
        int statusCode,
        IReadOnlyList<ErrorResponseHttp> errors)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(errors);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            // The first message is the summary; the whole set stays available below. Picking one
            // for `detail` beats leaving it null, which is what a caller reads first.
            Detail = errors.Count > 0 ? errors[0].ErrorMessage : null,
            Instance = controller.Request.Path
        };

        problem.Extensions[DomainErrorsExtension] = errors;

        // ObjectResult with the media type stated, not StatusCode(int, object). That overload
        // produces a bare ObjectResult with no ContentTypes, so content negotiation lands on the
        // JSON formatter's first media type — application/json. The framework's own error paths
        // (a bare NotFound(), model-state validation, both IExceptionHandlers) all send
        // application/problem+json, so business failures were the one family of errors answering
        // with a different content type than every other failure in the same API. ADR 0004 claims
        // the opposite in its consequences; this is the line that makes the claim true.
        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }

    /// <summary>
    /// The member carrying this API's own error codes on a problem document.
    /// </summary>
    /// <remarks>
    /// Not <c>errors</c>, which is the name RFC 7807's own <c>ValidationProblemDetails</c> uses for
    /// a map of field name to messages — and which the CQRS host produces for a FluentValidation
    /// failure. The two shapes were arriving under one name: <c>PUT /Trainer/me</c> with a
    /// malformed email answered <c>errors: {"ContactEmail": [...]}</c> on one host and
    /// <c>errors: [{errorCode, errorMessage}]</c> on the other, so a client that deserialised
    /// <c>errors</c> worked against one host and threw against the other.
    /// <para>
    /// Renaming the domain half is the cheaper of the two fixes and the more honest one: the field
    /// map is the standard's meaning of <c>errors</c>, and these codes are ours.
    /// </para>
    /// </remarks>
    public const string DomainErrorsExtension = "domainErrors";
}
