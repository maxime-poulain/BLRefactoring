using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using Yarp.ReverseProxy.Forwarder;

namespace TrainingHub.Blazor.Bff.Tests;

/// <summary>
/// Stands in for the REST API, and remembers what it was sent.
/// </summary>
/// <remarks>
/// The point of these tests is what the BFF puts on the wire — the access token, the path, the
/// absence of a credential when there is no session. That is only observable from the API's side of
/// the proxy, so the API is a handler rather than a process.
/// </remarks>
public sealed class RecordingHandler : HttpMessageHandler
{
    private readonly List<RecordedRequest> _requests = [];

    /// <summary>
    /// Requests.
    /// </summary>
    public IReadOnlyList<RecordedRequest> Requests => _requests;

    /// <summary>What the API answers. Defaults to an empty <c>200</c>.</summary>
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => Ok("{}");

    /// <summary>
    /// Ok.
    /// </summary>
    public static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>
    /// Send async.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization?.ToString(),
            request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken),
            request.Headers.AcceptLanguage.Count == 0
                ? null
                : request.Headers.AcceptLanguage.ToString()));

        return Respond(request);
    }

    /// <remarks>
    /// Deliberately inert. The handler is handed to <c>IHttpClientFactory</c>, which disposes the
    /// handlers it owns; a test reading <see cref="Requests"/> afterwards would otherwise be reading
    /// a disposed object.
    /// </remarks>
    [SuppressMessage("Usage", "CA2215:Dispose methods should call base class dispose",
        Justification = "Not calling base is the whole content of this override, and the reason is " +
                        "in the remarks above: the factory disposes the handlers it owns, and these " +
                        "tests read what the handler recorded after the client is gone. The rule is " +
                        "right about what the code does and wrong about whether it should.")]
    protected override void Dispose(bool disposing)
    {
    }
}

/// <summary>What the API saw, kept beyond the lifetime of the request message.</summary>
public sealed record RecordedRequest(
    HttpMethod Method,
    Uri? Uri,
    string? Authorization,
    string? Body = null,
    string? AcceptLanguage = null);

/// <summary>
/// Hands YARP the stub instead of a real socket.
/// </summary>
/// <remarks>
/// The proxy does not go through <c>IHttpClientFactory</c> — it builds its own invoker per cluster
/// — so replacing the client is the only way to observe a forwarded request without listening on a
/// port.
/// </remarks>
public sealed class StubForwarderHttpClientFactory(HttpMessageHandler handler) : IForwarderHttpClientFactory
{
    /// <summary>
    /// Create client.
    /// </summary>
    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => new(handler);
}
