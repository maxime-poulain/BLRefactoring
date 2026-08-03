namespace TrainingHub.Blazor.Client.Tests.Infrastructure;

/// <summary>
/// An <see cref="IHttpClientFactory"/> that answers from a function instead of the network.
/// </summary>
/// <remarks>
/// Everything the client-side infrastructure does goes through a named client, so a fake factory —
/// rather than a fake of each collaborator — is what lets a test say "the BFF answered 401" and
/// then read back which client was asked for and what was sent. The requests are kept because half
/// of what is worth proving here is about the request: the address, and the header that makes a
/// cookie-authenticated call unusable as a forgery.
/// </remarks>
internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        _respond = respond;

    /// <summary>The requests that reached the handler, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>The names the application asked the factory for, in order.</summary>
    public List<string> ClientNames { get; } = [];

    public HttpClient CreateClient(string name)
    {
        ClientNames.Add(name);

        return new HttpClient(new StubHandler(this))
        {
            // The real clients are configured with the host's own origin. Any absolute base does
            // here; what the tests read is the relative address the caller asked for.
            BaseAddress = new Uri("https://localhost/")
        };
    }

    private sealed class StubHandler(StubHttpClientFactory owner) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            owner.Requests.Add(request);

            return Task.FromResult(owner._respond(request));
        }
    }
}
