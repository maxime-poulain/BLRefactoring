using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using TrainingHub.Translations;

namespace TrainingHub.Shared.Api.Extensions;

/// <summary>
/// The language an API host answers in: resolved from the request, per request.
/// </summary>
/// <remarks>
/// Shared by both stacks for the reason CORS and logging are: a policy that lives in one host's
/// <c>Program.cs</c> only governs that host, and the other would quietly answer English to a
/// caller this one answers in French. The API reads <c>Accept-Language</c> alone — stateless, and
/// the one header every HTTP client can say a language in. The visitor's explicit choice lives in
/// a cookie at the BFF, which rewrites this header on the way through, so this layer never parses
/// a cookie that is not its own. See ADR 0088.
/// </remarks>
public static class LocalizationExtensions
{
    /// <summary>
    /// Registers the request-localization options: <c>Accept-Language</c> matched against the
    /// supported languages, falling back to the default.
    /// </summary>
    /// <remarks>
    /// The list and the default live in <see cref="SupportedLanguages"/> so the three hosts, the
    /// culture endpoint and the WebAssembly boot can never disagree. Formatting culture and
    /// language travel together — one list for both — because a French sentence around an
    /// English-formatted date is two languages on one line.
    /// </remarks>
    public static IServiceCollection AddApiLocalization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.Configure<RequestLocalizationOptions>(options =>
        {
            options.SetDefaultCulture(SupportedLanguages.Default);
            options.AddSupportedCultures([.. SupportedLanguages.All]);
            options.AddSupportedUICultures([.. SupportedLanguages.All]);
            // Accept-Language and nothing else. No cookie, because the BFF owns the visitor's
            // choice and says it in this header; no query string, because addresses stay
            // culture-independent (ADR 0088).
            options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
        });
    }

    /// <summary>
    /// Puts the culture resolution in the pipeline, reading the options registered by
    /// <see cref="AddApiLocalization"/>.
    /// </summary>
    public static IApplicationBuilder UseApiLocalization(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseRequestLocalization();
    }
}
