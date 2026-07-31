using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BLRefactoring.Shared.Api.Extensions;

/// <summary>
/// The browser-facing policy of an API host: which origins may call it.
/// </summary>
/// <remarks>
/// Shared by both stacks for the same reason as <c>AuthControllerBase</c> and
/// <c>ConcurrencyControllerExtensions</c>: a policy that lives in one host's <c>Program.cs</c>
/// only protects that host, and the other one silently ends up with no policy at all.
/// </remarks>
public static class CorsExtensions
{
    /// <summary>
    /// Name of the policy applied to every request of an API host.
    /// </summary>
    public const string PolicyName = "BlazorClient";

    private const string AllowedOriginsKey = "Cors:AllowedOrigins";

    /// <summary>
    /// Registers the CORS policy from the origins named in configuration.
    /// </summary>
    /// <remarks>
    /// The origins come from configuration rather than code, so a deployment names its own front
    /// end instead of requiring a rebuild. Deliberately no <c>AllowCredentials</c>: the JWT travels
    /// in the <c>Authorization</c> header, not in a cookie, so credentials buy nothing here. Adding
    /// it would also make ASP.NET Core reject any wildcard origin at startup, and the usual
    /// workaround for that error is a policy that reflects the caller's origin back — which allows
    /// everyone while looking restrictive.
    /// </remarks>
    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var allowedOrigins = ReadAllowedOrigins(configuration);

        return services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });
    }

    /// <summary>
    /// Applies the policy registered by <see cref="AddApiCors"/>, and warns when it allows nobody.
    /// </summary>
    /// <remarks>
    /// A warning rather than a throw, unlike <c>Jwt:Key</c>: an API is perfectly usable without a
    /// browser front end — the OpenAPI UI is same-origin and the integration tests run in-process.
    /// But staying silent would leave a misconfiguration to surface as an opaque CORS error in a
    /// browser console, which is a miserable thing to debug.
    /// </remarks>
    public static IApplicationBuilder UseApiCors(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();

        if (ReadAllowedOrigins(configuration).Length == 0)
        {
            // A static class cannot be a type argument, so the category is named explicitly
            // rather than obtained through ILogger<T>.
            app.ApplicationServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(CorsExtensions).FullName!)
                .LogWarning(
                    "No '{ConfigurationKey}' configured: every cross-origin browser request will " +
                    "be refused. Set the key if a browser front end needs to call this API.",
                    AllowedOriginsKey);
        }

        return app.UseCors(PolicyName);
    }

    private static string[] ReadAllowedOrigins(IConfiguration configuration)
        => configuration.GetSection(AllowedOriginsKey).Get<string[]>() ?? [];
}
