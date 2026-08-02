using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace BLRefactoring.Shared.Api.Errors;

/// <summary>
/// Answers a FluentValidation failure with a <see cref="ValidationProblemDetails"/>, keyed by
/// field name.
/// </summary>
/// <remarks>
/// The same body <c>[ApiController]</c> already produces when a data annotation rejects a request,
/// which is the point: two validation layers that disagree on their output shape force a client to
/// parse both. It used to answer <c>{ "errors": [{ "propertyName": …, "errorMessage": … }] }</c>,
/// a shape invented here and published nowhere.
/// <para>
/// Registered as an <see cref="IExceptionHandler"/> rather than a middleware. The two hand-written
/// middlewares it replaces had to be ordered relative to each other — the validation one inside the
/// global one, or a validation failure became a 500 — and that ordering was explained in five lines
/// of comment in each host. Handlers are tried in registration order and stop at the first that
/// returns <see langword="true"/>, so the ordering is the registration.
/// </para>
/// </remarks>
public sealed class ValidationExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not ValidationException validationException)
        {
            return false;
        }

        var failures = validationException.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray());

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ValidationProblemDetails(failures)
            {
                Status = StatusCodes.Status400BadRequest,
                Instance = httpContext.Request.Path
            }
        });
    }
}
