using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// Proves both health endpoints against the real dependencies, on both hosts.
/// </summary>
/// <remarks>
/// The suites run actual SQL Server, SeaweedFS and Mailpit containers, so a green
/// <c>/health/ready</c> here is an end-to-end fact about all four probes — the database, the
/// signed object-store read, the SMTP connect-and-quit and the poison gauge — not a mock's
/// opinion of them. The schema assertion is ADR 0037's no-secrets claim made executable: each
/// entry carries a name and a status, and any new field — a description, an exception, a
/// duration — fails the test before it reaches an anonymous endpoint.
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class HealthTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource
{
    /// <summary>
    /// Live endpoint, answers healthy, to an anonymous caller.
    /// </summary>
    [Fact]
    public async Task LiveEndpoint_AnswersHealthy_ToAnAnonymousCaller()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "liveness runs no checks: a 200 means the process is up and routing, and an " +
            "orchestrator holding no token must be able to ask");
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    /// <summary>
    /// Ready endpoint, names every dependency, and nothing else.
    /// </summary>
    [Fact]
    public async Task ReadyEndpoint_NamesEveryDependency_AndNothingElse()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the suite runs the real database, object store and mail relay, so readiness " +
            "should find every dependency reachable");

        using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        report.RootElement.GetProperty("status").GetString().Should().Be("Healthy");

        var checks = report.RootElement.GetProperty("checks").EnumerateArray().ToArray();

        checks.Select(check => check.GetProperty("name").GetString())
            .Should().BeEquivalentTo(["sql", "s3", "smtp", "outbox"],
                "readiness answers for exactly the world ADR 0037 names — database, object " +
                "store, mail relay, outbox — no more and no fewer");

        foreach (var check in checks)
        {
            check.GetProperty("status").GetString().Should().Be("Healthy");

            // The no-secrets claim, executable: a name, a status, and not one field more — no
            // description, no exception message, no duration on an anonymous endpoint.
            check.EnumerateObject().Select(property => property.Name)
                .Should().BeEquivalentTo(["name", "status"]);
        }
    }

    /// <summary>
    /// Dashboard, serves its page, in development.
    /// </summary>
    /// <remarks>
    /// Runs because <c>ApiFactory</c> hosts in Development — the only environment the dashboard
    /// exists in, by the same bargain as Scalar's reference UI (ADR 0037).
    /// </remarks>
    [Fact]
    public async Task Dashboard_ServesItsPage_InDevelopment()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/healthchecks-ui");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    /// <summary>
    /// Ui endpoint, speaks the dashboard format, in development.
    /// </summary>
    /// <remarks>
    /// The dashboard's own endpoint answers the same four probes as <c>/health/ready</c> but in
    /// the UI's format — an <c>entries</c> object keyed by check name, descriptions and durations
    /// included. That richer body is exactly what ADR 0037 keeps off the production surface,
    /// which is why this endpoint only exists where the page that reads it does.
    /// </remarks>
    [Fact]
    public async Task UiEndpoint_SpeaksTheDashboardFormat_InDevelopment()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/health/ui");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        report.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        report.RootElement.GetProperty("entries").EnumerateObject().Select(entry => entry.Name)
            .Should().BeEquivalentTo(["sql", "s3", "smtp", "outbox"],
                "the dashboard reads the same four probes readiness answers for");
    }
}
