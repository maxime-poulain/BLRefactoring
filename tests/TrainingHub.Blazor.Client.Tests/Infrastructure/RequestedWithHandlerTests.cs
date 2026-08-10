using System.Net;
using AwesomeAssertions;
using TrainingHub.Blazor.Client.Infrastructure;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Infrastructure;

/// <summary>
/// Behavior covered for the handler that marks every call as coming from this application.
/// </summary>
/// <remarks>
/// This header is a security control, not a convenience: the BFF authenticates with a cookie, and a
/// cookie travels on its own, which is what reintroduces cross-site request forgery. A cross-site
/// form or image cannot set a custom header and a cross-origin script attempting one faces a
/// preflight the host never approves. If this handler stopped adding it, nothing would fail to
/// compile and nothing would look wrong — the BFF would simply start refusing every call, or worse,
/// stop refusing forged ones if its own check were relaxed to match. See ADR 0009.
/// </remarks>
public sealed class RequestedWithHandlerTests
{
    /// <summary>
    /// Send, adds the header the BFF requires.
    /// </summary>
    [Fact]
    public async Task Send_AddsTheHeaderTheBffRequires()
    {
        // Arrange
        using var invoker = Invoker(out _);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost/bff/login");

        // Act
        using var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        request.Headers.GetValues(BffContract.RequestedWithHeader)
            .Should().ContainSingle().Which.Should().Be(BffContract.RequestedWithValue);
    }

    /// <summary>
    /// Send, every request, carries it.
    /// </summary>
    /// <remarks>
    /// The reason the header is attached by a handler rather than by the pages that change
    /// something: a handler cannot forget, and a page added next year would never be told.
    /// </remarks>
    [Fact]
    public async Task Send_EveryRequest_CarriesIt()
    {
        // Arrange
        using var invoker = Invoker(out var seen);
        using var read = new HttpRequestMessage(HttpMethod.Get, "https://localhost/bff/user");
        using var write = new HttpRequestMessage(HttpMethod.Post, "https://localhost/bff/logout");

        // Act
        (await invoker.SendAsync(read, CancellationToken.None)).Dispose();
        (await invoker.SendAsync(write, CancellationToken.None)).Dispose();

        // Assert
        seen.Should().HaveCount(2).And.AllSatisfy(request =>
            request.Headers.GetValues(BffContract.RequestedWithHeader)
                .Should().Equal(BffContract.RequestedWithValue));
    }

    private static HttpMessageInvoker Invoker(out List<HttpRequestMessage> seen)
    {
        var recorded = new List<HttpRequestMessage>();
        seen = recorded;

        return new HttpMessageInvoker(new RequestedWithHandler
        {
            InnerHandler = new RecordingHandler(recorded)
        });
    }

    private sealed class RecordingHandler(List<HttpRequestMessage> seen) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            seen.Add(request);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
