namespace BLRefactoring.Blazor.Client.Infrastructure;

/// <summary>
/// Marks every request as coming from this application.
/// </summary>
/// <remarks>
/// The BFF refuses anything without this header, so it goes on every call rather than on the ones
/// that happen to change something: a handler cannot forget, whereas a page can. The reasoning for
/// the header itself is on <see cref="BffContract.RequestedWithHeader"/>.
/// </remarks>
public sealed class RequestedWithHandler : DelegatingHandler
{
    /// <summary>
    /// Adds the <c>X-Requested-With</c> header every BFF call carries, which is what makes a
    /// cookie-authenticated request unusable as a cross-site forgery.
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Headers.Add(BffContract.RequestedWithHeader, BffContract.RequestedWithValue);

        return base.SendAsync(request, cancellationToken);
    }
}
