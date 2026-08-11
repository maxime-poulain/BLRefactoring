using System.Xml.Linq;
using TrainingHub.GeneratedClients;

namespace TrainingHub.Blazor.Bff;

/// <summary>
/// The doors a crawler reads: <c>robots.txt</c>, the sitemap, and the portraits Open Graph points
/// at (ADR 0073).
/// </summary>
/// <remarks>
/// Mapped at the root rather than under <c>/bff</c>, and past neither of the application's guards,
/// deliberately: a crawler sends neither the marker header nor <c>Sec-Fetch-Site</c>, and these
/// answers exist for exactly that caller. Nothing here weakens the <c>/api</c> guard either —
/// these are three narrow doors beside it, and every one of them answers from the catalog's
/// published, anonymous read: the sitemap through the same generated client the browser compiles,
/// the portraits through the API routes ADR 0063 already opened.
/// </remarks>
public static class CrawlerEndpoints
{
    // The catalog's own upper bound (PageRequest.MaxPageSize): the fewest round trips the API
    // allows for reading everything on offer.
    private const int SitemapPageSize = 100;

    // A ceiling on the paging loop, in case a broken answer ever says "next page" forever. A
    // hundred full pages is ten thousand addresses — room the catalog will not outgrow before
    // somebody revisits this file.
    private const int SitemapPageCap = 100;

    /// <summary>
    /// Maps the crawler's endpoints: <c>/robots.txt</c>, <c>/sitemap.xml</c> and the two portrait
    /// pass-throughs.
    /// </summary>
    public static IEndpointRouteBuilder MapCrawlerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/robots.txt", Robots).AllowAnonymous();

        endpoints.MapGet("/sitemap.xml", SitemapAsync).AllowAnonymous();

        endpoints.MapGet(
                "/portraits/trainings/{trainingId:guid}/{photoId:guid}", TrainingPortraitAsync)
            .AllowAnonymous();

        endpoints.MapGet(
                "/portraits/trainers/{trainerId:guid}/{photoId:guid}", TrainerPortraitAsync)
            .AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// What a crawler may read: everything but the spaces that are empty shells without a session.
    /// </summary>
    /// <remarks>
    /// Dynamic rather than a static file, because the sitemap line needs an absolute address and
    /// only the request knows the origin. Disallowing <c>/administration/</c>, <c>/trainings</c>
    /// and <c>/profile</c> costs a crawler nothing it could use — those pages answer a visitor
    /// with a sign-in redirect — and keeps it on the catalog, which is the half worth crawling.
    /// </remarks>
    private static IResult Robots(HttpContext httpContext)
    {
        var content = string.Join('\n',
            "User-agent: *",
            "Disallow: /administration/",
            "Disallow: /trainings",
            "Disallow: /profile",
            "",
            $"Sitemap: {Origin(httpContext)}/sitemap.xml");

        return Results.Text(content, "text/plain");
    }

    /// <summary>
    /// Every address the catalog currently offers: the listing, each training, each trainer with
    /// something on offer.
    /// </summary>
    /// <remarks>
    /// Read through the same published, anonymous search the browser uses — this host stays
    /// outside the catalog's door and mounts no reader of its own (ADR 0059). No
    /// <c>lastmod</c>, because the rows carry no date and inventing one would teach crawlers to
    /// trust a lie.
    /// </remarks>
    private static async Task<IResult> SitemapAsync(
        ICatalogClient catalogClient,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        List<CatalogTrainingHttpResponse> rows = [];

        try
        {
            var page = 1;
            PagedHttpResponseOfCatalogTrainingHttpResponse answer;

            do
            {
                answer = await catalogClient.SearchTrainingsAsync(
                    null, null, null, page, SitemapPageSize, cancellationToken);
                rows.AddRange(answer.Items);
                page++;
            }
            while (answer.HasNextPage && page <= SitemapPageCap);
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException)
        {
            // A sitemap that answers half the truth teaches a crawler that the missing half is
            // gone. When the catalog cannot be asked, the honest answer is "come back later".
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var origin = Origin(httpContext);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var urlset = new XElement(ns + "urlset",
            Entry(ns, $"{origin}/catalog"),
            rows.Select(row => Entry(ns, $"{origin}/catalog/{row.Id}")),
            rows.Select(row => row.TrainerId).Distinct()
                .Select(trainerId => Entry(ns, $"{origin}/catalog/trainers/{trainerId}")));

        // The declaration is spelled by hand: XDocument saved through a StringWriter stamps the
        // writer's own encoding — utf-16 — into a response that leaves as UTF-8.
        return Results.Text(
            $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n{urlset}", "application/xml");
    }

    private static Task<IResult> TrainingPortraitAsync(
        Guid trainingId,
        Guid photoId,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        ForwardPortraitAsync(
            $"/Catalog/trainings/{trainingId}/photo/{photoId}",
            httpClientFactory, httpContext, cancellationToken);

    private static Task<IResult> TrainerPortraitAsync(
        Guid trainerId,
        Guid photoId,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        ForwardPortraitAsync(
            $"/Catalog/trainers/{trainerId}/photo/{photoId}",
            httpClientFactory, httpContext, cancellationToken);

    /// <summary>
    /// Fetches a portrait from the API's public route and hands it back: status, content type and
    /// bytes.
    /// </summary>
    /// <remarks>
    /// The pass-through exists because <c>og:image</c> is fetched by link unfurlers, and the
    /// <c>/api</c> guard asks for headers no unfurler sends — an image address only browsers can
    /// read is worse than none. The alternative, widening the guard itself, would reopen the
    /// argument ADR 0063 settled; a narrow door beside it does not.
    /// </remarks>
    private static async Task<IResult> ForwardPortraitAsync(
        string portraitPath,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(BffEndpoints.ApiClientName);

        try
        {
            using var response = await client.GetAsync(portraitPath, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // The API's own verdict, passed through — a photo that was replaced answers 404
                // here for the same reason it does there.
                return Results.StatusCode((int)response.StatusCode);
            }

            // The address carries the photo's identity, so it stops resolving when the picture is
            // replaced rather than ever serving a stale one — which is what lets a year of
            // immutable be honest (the API's own route makes the same promise, ADR 0063).
            httpContext.Response.Headers.CacheControl = "public, max-age=31536000, immutable";

            return Results.Bytes(
                await response.Content.ReadAsByteArrayAsync(cancellationToken),
                response.Content.Headers.ContentType?.ToString());
        }
        catch (HttpRequestException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static string Origin(HttpContext httpContext) =>
        $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

    private static XElement Entry(XNamespace ns, string location) =>
        new(ns + "url", new XElement(ns + "loc", location));
}
