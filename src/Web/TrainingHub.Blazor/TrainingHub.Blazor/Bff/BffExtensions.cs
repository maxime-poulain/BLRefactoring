using TrainingHub.Blazor.Client.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace TrainingHub.Blazor.Bff;

/// <summary>
/// Turns this host into a backend for frontend: it holds the access token and forwards to the API,
/// so the browser never sees a credential it could leak.
/// </summary>
/// <remarks>
/// The front end used to keep its JWT in <c>localStorage</c> and call the API cross-origin. Any
/// script running in the page — including one arriving through a compromised dependency — could
/// read that token and replay it anywhere, for its whole lifetime. A portable credential in reach
/// of every XSS.
/// <para>
/// Now the token lives in an authentication cookie that JavaScript cannot read, the browser talks
/// only to this origin, and this host attaches the token on the way out. The win is not the cookie
/// as such: it is that the token is never in the browser. A cookie carrying a token readable by
/// script would be worth exactly as much as the localStorage it replaced.
/// </para>
/// <para>
/// What this does not do is stop an attacker who already runs script in the page from making
/// authenticated calls — the cookie rides along on requests they trigger. The attack is bounded to
/// the session and this origin instead of yielding a token they can take away.
/// </para>
/// <para>
/// See ADR 0009.
/// </para>
/// </remarks>
public static class BffExtensions
{
    /// <summary>
    /// Name under which the API's access token is kept inside the authentication cookie.
    /// </summary>
    public const string AccessTokenName = "access_token";

    /// <summary>Prefix under which this host forwards to the API.</summary>
    public const string ApiPrefix = "/api";

    private const string ApiBaseAddressKey = "Api:BaseAddress";

    private const string RouteAndClusterId = "api";

    /// <summary>
    /// Registers cookie authentication and the reverse proxy that fronts the API.
    /// </summary>
    public static IServiceCollection AddBff(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var apiBaseAddress = configuration[ApiBaseAddressKey]
            ?? throw new InvalidOperationException(
                $"Missing configuration value '{ApiBaseAddressKey}'. The BFF forwards to the API " +
                "and has no sensible default: a localhost address in production would fail " +
                "obscurely, and silently.");

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                // Not readable by script: the entire point of moving off localStorage.
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                // Strict rather than Lax: nothing in this application expects to arrive
                // authenticated from another site, so there is no flow to accommodate.
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Name = "__Host-bff";

                // The cookie dies with the token it carries, and nothing extends it. Sliding is on
                // by default: the handler renews the ticket once past half its window, granting a
                // fresh full window each time — so the session outlives the JWT it was cut from,
                // and the user stays apparently signed in while every forwarded call comes back
                // 401. ADR 0009 says this is off; until now only the ExpiresUtc half of that
                // sentence was written. There is no refresh token to reach for: the API issues one
                // credential, with one lifetime, and the honest end of it is signing in again.
                options.SlidingExpiration = false;

                // The caller is a fetch client, not a navigating browser. Answer status codes
                // rather than redirecting to a login page that does not exist server-side.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization();

        // The API client this host uses for the endpoints it serves itself — sign-in, mostly.
        // Forwarded traffic does not go through it; YARP handles that.
        services.AddHttpClient(BffEndpoints.ApiClientName, client =>
            client.BaseAddress = new Uri(apiBaseAddress));

        services
            .AddReverseProxy()
            .LoadFromMemory(BuildRoutes(), BuildClusters(apiBaseAddress))
            .AddTransforms(context => context.AddRequestTransform(async transform =>
            {
                // The whole point of the proxy: the credential is attached here, server-side,
                // from the cookie — never handed to the page that triggered the call.
                var token = await transform.HttpContext.GetTokenAsync(AccessTokenName);

                if (!string.IsNullOrEmpty(token))
                {
                    transform.ProxyRequest.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
            }));

        return services;
    }

    /// <summary>
    /// Puts the BFF in the pipeline: authentication, the forgery guard, then the proxy.
    /// </summary>
    public static WebApplication UseBff(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Before authentication, deliberately. Forwarded calls need the same forgery guard as the
        // endpoints below — more so, since they are the ones that write — and a request that should
        // not have been made is worth refusing before any work is done on it. The proxy has no
        // endpoint filter to hang this on, so it is ordinary middleware, scoped to the prefix.
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments(ApiPrefix, StringComparison.Ordinal),
            branch => branch.Use(async (context, next) =>
            {
                if (!IsFromThisApplication(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                await next(context);
            }));

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapBffEndpoints();
        app.MapReverseProxy();

        return app;
    }

    /// <summary>
    /// Whether the request carries this application's marker header.
    /// </summary>
    /// <remarks>
    /// See <see cref="BffContract.RequestedWithHeader"/> for why a header is the control.
    /// </remarks>
    internal static bool IsFromThisApplication(HttpRequest request) =>
        string.Equals(
            request.Headers[BffContract.RequestedWithHeader],
            BffContract.RequestedWithValue,
            StringComparison.Ordinal);

    private static IReadOnlyList<RouteConfig> BuildRoutes() =>
    [
        new RouteConfig
        {
            RouteId = RouteAndClusterId,
            ClusterId = RouteAndClusterId,
            // Authorization is required here, not left to the API: an anonymous call would be
            // forwarded without a token and answered 401 anyway, one network hop later.
            AuthorizationPolicy = "default",
            Match = new RouteMatch { Path = $"{ApiPrefix}/{{**catch-all}}" }
        }
        .WithTransformPathRemovePrefix(ApiPrefix)
    ];

    private static IReadOnlyList<ClusterConfig> BuildClusters(string apiBaseAddress) =>
    [
        new ClusterConfig
        {
            ClusterId = RouteAndClusterId,
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["api"] = new DestinationConfig { Address = apiBaseAddress }
            }
        }
    ];
}
