using AwesomeAssertions;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// Proves the CORS policy actually filters origins, rather than merely being configured.
/// </summary>
/// <remarks>
/// Run against both suites, because the defect it guards was one host having no CORS configuration
/// at all — no policy, no middleware, no configuration key — while the other had a tested one. A
/// test that covers one of two hosts covers neither, and this file existed twice, byte for byte,
/// to say so.
/// <para>
/// The origins asserted here are the ones both <c>appsettings.Development.json</c> declare, which
/// is the environment the fixture runs its host in.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class CorsTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource
{
    private const string AllowedOrigin = "https://localhost:7067";
    private const string ForeignOrigin = "https://evil.example.com";

    /// <summary>
    /// Configured origin, is allowed.
    /// </summary>
    [Fact]
    public async Task ConfiguredOrigin_IsAllowed()
    {
        var client = Factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/Trainer/me");
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await client.SendAsync(request);

        // The endpoint answers 401 without a token, which is irrelevant here: the CORS
        // middleware runs before authentication and stamps the header either way.
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be(AllowedOrigin);
    }

    /// <summary>
    /// Foreign origin, is not allowed.
    /// </summary>
    [Fact]
    public async Task ForeignOrigin_IsNotAllowed()
    {
        var client = Factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/Trainer/me");
        request.Headers.Add("Origin", ForeignOrigin);

        var response = await client.SendAsync(request);

        // No header at all, rather than one naming the caller. A policy that echoed the origin
        // back would let everyone through while looking restrictive.
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    /// <summary>
    /// Preflight, from foreign origin, is not approved.
    /// </summary>
    [Fact]
    public async Task Preflight_FromForeignOrigin_IsNotApproved()
    {
        var client = Factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/Trainer/me");
        request.Headers.Add("Origin", ForeignOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
