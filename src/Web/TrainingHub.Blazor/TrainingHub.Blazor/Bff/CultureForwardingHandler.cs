using Microsoft.AspNetCore.Localization;

namespace TrainingHub.Blazor.Bff;

/// <summary>
/// Says the request's resolved culture on every call this host makes to the API itself.
/// </summary>
/// <remarks>
/// The proxy's transform covers forwarded traffic; this covers the other channel — the named
/// client the BFF's own endpoints and the prerendering read clients go through. Same sentence,
/// same header: the API reads <c>Accept-Language</c> alone, and a catalog fetched without this
/// during the prerendered pass would be asked for in no language at all while the page around it
/// answers in the visitor's (ADR 0088). A call with no request behind it keeps whatever the
/// client set, which today is nothing.
/// </remarks>
public sealed class CultureForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    /// <summary>
    /// Restates the resolved culture as the outgoing <c>Accept-Language</c> header.
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var culture = httpContextAccessor.HttpContext?.Features.Get<IRequestCultureFeature>();

        if (culture is not null)
        {
            request.Headers.AcceptLanguage.Clear();
            request.Headers.AcceptLanguage.ParseAdd(culture.RequestCulture.UICulture.Name);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
