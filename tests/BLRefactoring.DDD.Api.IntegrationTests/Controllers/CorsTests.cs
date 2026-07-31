using AwesomeAssertions;
using BLRefactoring.DDD.Api.IntegrationTests.Fixtures;
using Xunit;

namespace BLRefactoring.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// Proves the CORS policy actually filters origins, rather than merely being configured.
/// </summary>
/// <remarks>
/// The origins asserted here are the ones in the API's appsettings.Development.json, which the
/// test host loads. Without these tests the policy could silently allow everything again — the
/// state this API was in — and nothing would fail.
/// </remarks>
[Collection("Api")]
public class CorsTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const string AllowedOrigin = "https://localhost:7067";
    private const string ForeignOrigin = "https://evil.example.com";

    [Fact]
    public async Task ConfiguredOrigin_IsAllowed()
    {
        var client = Factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/Trainer/all");
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await client.SendAsync(request);

        // The endpoint answers 401 without a token, which is irrelevant here: the CORS
        // middleware runs before authentication and stamps the header either way.
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be(AllowedOrigin);
    }

    [Fact]
    public async Task ForeignOrigin_IsNotAllowed()
    {
        var client = Factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/Trainer/all");
        request.Headers.Add("Origin", ForeignOrigin);

        var response = await client.SendAsync(request);

        // No header at all, rather than one naming the caller. A policy that echoed the origin
        // back would let everyone through while looking restrictive.
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Preflight_FromForeignOrigin_IsNotApproved()
    {
        var client = Factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/Trainer/all");
        request.Headers.Add("Origin", ForeignOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
