using AwesomeAssertions;
using TrainingHub.Blazor.Client.Infrastructure;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Infrastructure;

/// <summary>
/// The one-shot nature of the token's ride, pinned (ADR 0083).
/// </summary>
/// <remarks>
/// The handler sits on every API call, so what is worth proving is the division: the request after
/// a deposit carries the header, and the very next one carries nothing — a token is single-use on
/// Cloudflare's side, and a handler that re-sent a spent one would turn every later call into a
/// refusal.
/// </remarks>
public sealed class TurnstileTokenHandlerTests
{
    /// <summary>
    /// Send async, a token deposited, attaches it to one request and forgets it.
    /// </summary>
    [Fact]
    public async Task SendAsync_ATokenDeposited_AttachesItToOneRequestAndForgetsIt()
    {
        var accessor = new TurnstileTokenAccessor();
        var seen = new List<string?>();
        using var client = new HttpClient(new TurnstileTokenHandler(accessor)
        {
            InnerHandler = new HeaderReader(seen)
        })
        {
            BaseAddress = new Uri("https://localhost/")
        };

        accessor.Deposit("token-the-widget-minted");

        await client.GetAsync("api/first");
        await client.GetAsync("api/second");

        seen.Should().Equal(
            ["token-the-widget-minted", null],
            "the deposit rides the next request and only that one");
    }

    private sealed class HeaderReader(List<string?> seen) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            seen.Add(request.Headers.TryGetValues(BffContract.TurnstileTokenHeader, out var values)
                ? values.Single()
                : null);

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
