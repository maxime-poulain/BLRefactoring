using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TrainingHub.Shared.Api.Health;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Health;

/// <summary>
/// Holds the readiness body to names and statuses, and nothing else.
/// </summary>
/// <remarks>
/// The integration facts prove the happy path — every dependency up, every field present. What
/// they cannot prove is what the body looks like when a probe has just thrown, because the suites
/// run their dependencies for real. So this test builds the worst report by hand — an exception
/// with a message worth stealing, descriptions on every entry — and asserts the writer keeps all
/// of it out of the JSON. This is ADR 0037's no-secrets claim at the only layer that could break
/// it.
/// </remarks>
public sealed class HealthResponseWriterTests
{
    private const string SecretException = "Login failed for user 'sa' at 10.0.0.12:1433";
    private const string SecretDescription = "3 poison message(s) sit in the outbox awaiting an operator.";

    /// <summary>
    /// A report full of secrets, is written as names and statuses only.
    /// </summary>
    [Fact]
    public async Task AReportFullOfSecrets_IsWrittenAsNamesAndStatusesOnly()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["sql"] = new(
                    HealthStatus.Unhealthy,
                    SecretDescription,
                    TimeSpan.FromMilliseconds(37),
                    new InvalidOperationException(SecretException),
                    data: null),
                ["outbox"] = new(
                    HealthStatus.Degraded,
                    SecretDescription,
                    TimeSpan.FromMilliseconds(4),
                    exception: null,
                    data: null),
            },
            HealthStatus.Unhealthy,
            TimeSpan.FromMilliseconds(41));

        var httpContext = new DefaultHttpContext();
        using var body = new MemoryStream();
        httpContext.Response.Body = body;

        await HealthResponseWriter.WriteAsync(httpContext, report);

        body.Position = 0;
        using var reader = new StreamReader(body);
        var json = await reader.ReadToEndAsync();

        // Nothing the report carried beyond names and statuses may survive serialization: not the
        // exception's message, not a description, not a duration.
        json.Should().NotContain(SecretException);
        json.Should().NotContain(SecretDescription);

        using var document = JsonDocument.Parse(json);

        document.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(["status", "checks"]);

        document.RootElement.GetProperty("status").GetString().Should().Be("Unhealthy");

        var checks = document.RootElement.GetProperty("checks").EnumerateArray().ToArray();

        checks.Select(check => check.GetProperty("name").GetString())
            .Should().BeEquivalentTo(["sql", "outbox"]);

        foreach (var check in checks)
        {
            check.EnumerateObject().Select(property => property.Name)
                .Should().BeEquivalentTo(["name", "status"]);
        }

        checks.Single(check => check.GetProperty("name").GetString() == "sql")
            .GetProperty("status").GetString().Should().Be("Unhealthy");
        checks.Single(check => check.GetProperty("name").GetString() == "outbox")
            .GetProperty("status").GetString().Should().Be("Degraded");
    }

    /// <summary>
    /// The content type, is json.
    /// </summary>
    [Fact]
    public async Task TheContentType_IsJson()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(), HealthStatus.Healthy, TimeSpan.Zero);

        var httpContext = new DefaultHttpContext();
        using var body = new MemoryStream();
        httpContext.Response.Body = body;

        await HealthResponseWriter.WriteAsync(httpContext, report);

        httpContext.Response.ContentType.Should().Be("application/json; charset=utf-8");
    }
}
