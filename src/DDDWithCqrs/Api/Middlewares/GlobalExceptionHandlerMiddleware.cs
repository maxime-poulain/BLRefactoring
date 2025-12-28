using System.Text;

namespace BLRefactoring.DDDWithCqrs.Api.Middlewares;

public class GlobalExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            await HandleUncaughtException(context, e);
        }
    }

    private async Task HandleUncaughtException(HttpContext context, Exception exception)
    {
        await LogUnhandledException(context, exception);

        const string errorResponse = "An expected error occurred while processing the request. Try again later.";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(errorResponse);
    }

    private async Task LogUnhandledException(HttpContext context, Exception exception)
    {
        const string errorMessage =
            "An unhandled exception occurred at `{endPointPath}` with following request: `{Payload}`";
        string endpoint = context.Request.Path; // Retrieve the endpoint (action) that failed
        var httpRequest = await FormatHttpRequest(context.Request); // Format the HTTP request details
        logger.LogError(exception, errorMessage, endpoint, httpRequest);
    }

    private static Task<string> FormatHttpRequest(HttpRequest request)
    {
        var requestDetails = new StringBuilder();

        requestDetails.Append("HTTP Method: ")
            .AppendLine(request.Method);

        requestDetails.Append("URL: ")
            .Append(request.Scheme)
            .Append("://")
            .Append(request.Host)
            .Append(request.Path)
            .Append(request.QueryString)
            .AppendLine();

        return Task.FromResult(requestDetails.ToString());
    }
}
