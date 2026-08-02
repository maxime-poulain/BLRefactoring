using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BLRefactoring.Shared.Api.Errors;

/// <summary>
/// Last resort: logs whatever reached here and answers a <see cref="ProblemDetails"/> 500.
/// </summary>
/// <remarks>
/// Registered last, so it only sees what no other handler claimed.
/// <para>
/// The body used to be a bare JSON string — <c>WriteAsJsonAsync</c> applied to a
/// <see langword="const"/> <see cref="string"/>, which serialises to <c>"An unexpected error…"</c>,
/// quotes included: not an object, no status, no field to read. A client had no way to treat it
/// like any other failure of the same API.
/// </para>
/// <para>
/// What is logged and what is answered are deliberately different. The log gets the exception, the
/// endpoint and the query string, because that is what makes an incident diagnosable; the response
/// gets a fixed sentence, because an exception message can carry a table name, a connection string
/// or a value the caller was never entitled to see.
/// </para>
/// <para>
/// The layered host had no such handler at all: an unhandled exception left as whatever the host
/// happened to produce, which in Development is a page of stack trace. Living in
/// <c>Shared.Api</c> is what stops the two hosts differing on it.
/// </para>
/// </remarks>
public sealed class UnhandledExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        logger.LogError(
            exception,
            "Unhandled exception while handling {Method} {Path}{QueryString}.",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.Request.QueryString);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "The request could not be processed. Try again later.",
                Instance = httpContext.Request.Path
            }
        });
    }
}
