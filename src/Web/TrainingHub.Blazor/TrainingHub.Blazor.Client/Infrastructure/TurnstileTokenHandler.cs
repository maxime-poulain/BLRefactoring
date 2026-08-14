namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Attaches the waiting Turnstile token to the request that spends it (ADR 0083).
/// </summary>
/// <remarks>
/// The counterpart of <see cref="RequestedWithHandler"/>, for the one header that is per-message
/// rather than universal: most requests find nothing deposited and carry nothing extra. The token
/// travels as a header so the body stays exactly the contract the API publishes — the reasoning is
/// on <see cref="BffContract.TurnstileTokenHeader"/>.
/// </remarks>
public sealed class TurnstileTokenHandler(TurnstileTokenAccessor accessor) : DelegatingHandler
{
    /// <summary>
    /// Takes the deposited token, when there is one, and sends it along as the header the BFF
    /// judges before forwarding a contact message.
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = accessor.Take();

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Add(BffContract.TurnstileTokenHeader, token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
