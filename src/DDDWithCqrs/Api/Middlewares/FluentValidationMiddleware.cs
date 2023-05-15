using System.Net;
using System.Net.Mime;
using FluentValidation;

namespace BLRefactoring.DDDWithCqrs.Api.Middlewares;

public class FluentValidationMiddleware
{
    private readonly RequestDelegate _next;

    public FluentValidationMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException e)
        {
            await HandleFluentValidationException(context, e);
        }
    }

    private static async Task HandleFluentValidationException(HttpContext context, ValidationException e)
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        var failures = e.Errors;
        var errorResponse = new
        {
            Errors = failures.Select(validationFailure => new
            {
                validationFailure.PropertyName,
                validationFailure.ErrorMessage
            })
        };

        await context.Response.WriteAsJsonAsync(errorResponse);
    }
}
