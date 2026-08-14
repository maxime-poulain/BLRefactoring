using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TrainingHub.Blazor.Client.Infrastructure;
using TrainingHub.GeneratedClients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace TrainingHub.Blazor.Bff;

/// <summary>
/// The endpoints the front end talks to directly. Everything else is forwarded.
/// </summary>
/// <remarks>
/// Sign-in cannot be forwarded like the rest: the API answers <c>/Auth/login</c> with the token
/// itself, and handing that response back to the browser would undo the entire arrangement. So this
/// host calls the API, keeps the token, and returns nothing but a cookie.
/// <para>
/// Registration could be forwarded — its response carries no credential — and deliberately is not:
/// the proxy's anonymous family is exactly the catalog's controllers (ADR 0062), and widening that
/// family for one POST would be a bigger door than one explicit endpoint beside sign-in. The
/// account conversation belongs to this host either way.
/// </para>
/// <para>
/// The contact message is the reverse case: it <em>was</em> forwarded, and deliberately no longer
/// is (ADR 0083). Its endpoint stands at the very address the proxy used to serve, because the
/// message now pays a toll on the way through — a Turnstile token, judged here, where the secret
/// key lives and the visitor's connection ends — and a proxied twin of the path would be a way
/// around the toll booth.
/// </para>
/// </remarks>
public static class BffEndpoints
{
    /// <summary>Named client this host uses to reach the API for its own calls.</summary>
    /// <remarks>
    /// Not <c>"Api"</c>. The WebAssembly application registers a client under that name, pointed at
    /// its own origin, and the two configurations merged when this host registered the browser's
    /// services — leaving sign-in to build the browser's client and fail. The host no longer
    /// registers them, and this name no longer invites the same accident.
    /// </remarks>
    public const string ApiClientName = "BffApi";

    /// <summary>
    /// Maps the endpoints the browser talks to: sign-in, sign-out, and the forwarding proxy.
    /// </summary>
    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var bff = endpoints.MapGroup("/bff").AddEndpointFilter(RejectCrossSiteRequests);

        bff.MapPost("/login", LoginAsync).AllowAnonymous();

        bff.MapPost("/register", RegisterAsync).AllowAnonymous();

        // The cast is load-bearing. LogoutAsync takes one HttpContext and returns a Task, which is
        // RequestDelegate's shape exactly, and that overload wins over the route-handler one: the
        // returned IResult is discarded — hence a 200 where 204 was written — and, far worse, the
        // request never goes through the route-handler pipeline, so the group's forgery filter does
        // not apply to it. A cross-site POST could sign the user out. The compiler warns (ASP0016)
        // and the warning is easy to read past; the suite now asks for the 403 directly.
        //
        // The other two are safe by accident of shape: GetUser returns IResult rather than a Task,
        // and LoginAsync takes parameters RequestDelegate has no room for.
        bff.MapPost("/logout", (Delegate)LogoutAsync).RequireAuthorization();

        bff.MapGet("/user", GetUser).AllowAnonymous();

        bff.MapGet("/turnstile", GetTurnstileSettings).AllowAnonymous();

        // Outside the /bff group, at the address the proxy used to forward: the template outranks
        // the catch-all route, so every contact message lands here and none slips past the token
        // judgment below (ADR 0083). The forgery guard still applies — the /api middleware demands
        // the application's header on every write — and the per-visitor window moves here with the
        // endpoint, since this is now where the visitor's messages arrive.
        endpoints.MapPost(
                $"{BffExtensions.ApiPrefix}/{BffExtensions.AnonymousSegment}/trainers/{{trainerId:guid}}/contact",
                ContactTrainerAsync)
            .AllowAnonymous()
            .RequireRateLimiting(BffExtensions.ContactWindowPolicy);

        return endpoints;
    }

    /// <summary>
    /// Refuses anything that did not come from this application's own front end.
    /// </summary>
    private static async ValueTask<object?> RejectCrossSiteRequests(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (!BffExtensions.IsFromThisApplication(context.HttpContext.Request))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }

    /// <summary>
    /// Exchanges credentials for a cookie. The token stays here.
    /// </summary>
    private static async Task<IResult> LoginAsync(
        LoginHttpRequest credentials,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ApiClientName);

        var response = await client.PostAsJsonAsync("/Auth/login", credentials, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // The API's own verdict is passed through — a wrong password is its judgment to make.
            // Its problem document is not: it describes a call the browser never made, to an
            // address it does not know. The status is the part that means something here.
            return Results.StatusCode((int)response.StatusCode);
        }

        var body = await response.Content.ReadFromJsonAsync<LoginHttpResponse>(cancellationToken);

        if (string.IsNullOrWhiteSpace(body?.Token))
        {
            return Results.Problem("The API accepted the credentials but returned no token.");
        }

        var token = new JwtSecurityTokenHandler().ReadJwtToken(body.Token);

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(Canonical(token.Claims), CookieAuthenticationDefaults.AuthenticationScheme));

        var properties = new AuthenticationProperties
        {
            // The cookie dies with the token it carries. Left to the default — fourteen days,
            // sliding — the session would outlive the token by a wide margin, and the user would
            // stay apparently signed in while every forwarded call came back 401. There is no
            // refresh token to reach for: the API issues one credential, with one lifetime.
            ExpiresUtc = token.ValidTo,
            IsPersistent = false
        };
        properties.StoreTokens([new AuthenticationToken { Name = BffExtensions.AccessTokenName, Value = body.Token }]);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);

        return Results.NoContent();
    }

    /// <summary>
    /// Creates an account, on behalf of a visitor who by definition has no session yet.
    /// </summary>
    /// <remarks>
    /// Unlike sign-in, the API's problem document is passed through, body and content type alike:
    /// it describes the very fields the browser submitted — a taken username, a refused password —
    /// and the form reads the per-field messages out of it. The 201 is passed without its
    /// <c>Location</c>: the address points into the API's own origin, which the browser cannot
    /// reach, and the page navigates to sign-in rather than to the trainer it just became.
    /// </remarks>
    private static async Task<IResult> RegisterAsync(
        RegisterHttpRequest registration,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ApiClientName);

        var response = await client.PostAsJsonAsync("/Auth/register", registration, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return Results.StatusCode(StatusCodes.Status201Created);
        }

        var problem = await response.Content.ReadAsStringAsync(cancellationToken);

        return Results.Content(
            problem,
            response.Content.Headers.ContentType?.ToString(),
            statusCode: (int)response.StatusCode);
    }

    /// <summary>
    /// Carries a visitor's message to the API, once their Turnstile token holds up.
    /// </summary>
    /// <remarks>
    /// The token rides a header rather than the body, so what is forwarded is exactly the contract
    /// the API publishes — the API never learns a challenge existed (ADR 0083). A missing token is
    /// refused without asking Cloudflare anything, and a refused one answers the same 403, worded
    /// for the person who sees it: a visitor whose widget expired, not only a bot. The API's own
    /// verdicts — the 404 of a trainer the catalog will not show, the 400 of a malformed message,
    /// the 429 of a buried recipient — pass through body and status alike, because the form reads
    /// the per-field messages out of the problem document the way registration's does.
    /// <para>
    /// With no key pair configured there is no judgment to make: an operator who supplied neither
    /// key chose to run without the challenge, and the message is forwarded as it was before the
    /// challenge existed — the honeypot and both rate windows still standing (ADR 0082, ADR 0083).
    /// </para>
    /// </remarks>
    private static async Task<IResult> ContactTrainerAsync(
        Guid trainerId,
        ContactTrainerHttpRequest message,
        [FromHeader(Name = BffContract.TurnstileTokenHeader)] string? turnstileToken,
        ITurnstileVerifier turnstile,
        IOptions<TurnstileOptions> options,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (options.Value.IsConfigured
            && (string.IsNullOrWhiteSpace(turnstileToken)
                || !await turnstile.IsGenuineAsync(
                    turnstileToken,
                    httpContext.Connection.RemoteIpAddress?.ToString(),
                    cancellationToken)))
        {
            return Results.Problem(
                title: "The anti-robot check did not pass.",
                detail: "Complete the check in the form and send your message again.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var client = httpClientFactory.CreateClient(ApiClientName);

        var response = await client.PostAsJsonAsync(
            $"/{BffExtensions.AnonymousSegment}/trainers/{trainerId}/contact",
            message,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return Results.StatusCode(StatusCodes.Status202Accepted);
        }

        var problem = await response.Content.ReadAsStringAsync(cancellationToken);

        return Results.Content(
            problem,
            response.Content.Headers.ContentType?.ToString(),
            statusCode: (int)response.StatusCode);
    }

    /// <summary>
    /// The public half of the Turnstile key pair, for the widget to render with.
    /// </summary>
    /// <remarks>
    /// The browser has to be told the site key somehow, and the WebAssembly application carries no
    /// configuration of its own (ADR 0035): this endpoint is the one channel, anonymous because the
    /// key is public by design — it is in the page source of every site that renders the widget.
    /// With no pair configured the answer is a null key, which the dialog reads as "no challenge
    /// to render" (ADR 0083); there is always a document, and it always parses, for the reason
    /// <see cref="GetUser"/> gives.
    /// </remarks>
    private static IResult GetTurnstileSettings(IOptions<TurnstileOptions> options) =>
        Results.Ok(new TurnstileSettings(
            options.Value.IsConfigured ? options.Value.SiteKey : null));

    private static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Results.NoContent();
    }

    /// <summary>
    /// Who the caller is, as the cookie says. The front end's only source of identity.
    /// </summary>
    /// <remarks>
    /// Anonymous rather than authorized: "nobody is signed in" is an ordinary answer to this
    /// question, not a failure, and making it a 401 would have every page start by handling an
    /// error.
    /// <para>
    /// A signed-out caller gets a document with no claims rather than a <c>null</c> one. Returning
    /// null looks equivalent and is not: the framework writes *nothing* for a null value, so the
    /// answer is a 200 with an empty body, and reading that as JSON on the other side is an
    /// exception rather than an absence. There is always a document, and it always parses.
    /// </para>
    /// </remarks>
    private static IResult GetUser(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Results.Ok(new BffUser([]));
        }

        return Results.Ok(new BffUser(
            [.. httpContext.User.Claims.Select(claim => new BffClaim(claim.Type, claim.Value))]));
    }

    /// <summary>
    /// Translates the short JWT claim types back to the canonical ones.
    /// </summary>
    /// <remarks>
    /// The API wrote <see cref="ClaimTypes.Name"/> and the serializer shortened it to
    /// <c>unique_name</c>; likewise <c>role</c>. Without undoing that, <c>User.Identity.Name</c> and
    /// <c>AuthorizeView Roles</c> both come up empty, on either side of the cookie. The framework
    /// does this when it *validates* a token, which is not what happens here.
    /// <para>
    /// Not validating is correct: the token was just obtained from the API over a trusted channel,
    /// and the API validates it again on every forwarded call. Reading it only turns it into claims
    /// so the front end can render a name.
    /// </para>
    /// </remarks>
    private static IEnumerable<Claim> Canonical(IEnumerable<Claim> claims) =>
        claims.Select(claim =>
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.TryGetValue(claim.Type, out var canonical)
                ? new Claim(canonical, claim.Value, claim.ValueType)
                : claim);
}
