using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TrainingHub.Shared.Api.Health;

/// <summary>
/// Serializes a health report as check names and statuses, and nothing else.
/// </summary>
/// <remarks>
/// The framework's default body is the single word <c>Healthy</c>; the alternative most examples
/// reach for serializes the whole report — descriptions, durations, exception messages — onto an
/// anonymous endpoint. This writer is the middle ADR 0037 decides on: enough structure for a poll
/// to see which dependency went down, and no field an attacker could read a stack trace or a
/// connection string out of. What it omits is the point, which is why a unit test feeds it a
/// report loaded with exceptions and holds the JSON to these two properties — public, like the
/// enricher and the exception handler, because that test names it.
/// </remarks>
public static class HealthResponseWriter
{
    /// <summary>
    /// Writes <c>{"status": ..., "checks": [{"name": ..., "status": ...}]}</c> to the response.
    /// </summary>
    /// <param name="httpContext">The response being answered.</param>
    /// <param name="report">The report the health service produced.</param>
    /// <returns>A task that completes when the body is written.</returns>
    public static Task WriteAsync(HttpContext httpContext, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(report);

        var body = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries
                .Select(entry => new { name = entry.Key, status = entry.Value.Status.ToString() })
        });

        httpContext.Response.ContentType = "application/json; charset=utf-8";

        // RequestAborted rather than an unspoken None: the delegate the framework accepts carries
        // no token of its own, and a caller that hung up is the one reason to stop writing.
        return httpContext.Response.WriteAsync(body, httpContext.RequestAborted);
    }
}
