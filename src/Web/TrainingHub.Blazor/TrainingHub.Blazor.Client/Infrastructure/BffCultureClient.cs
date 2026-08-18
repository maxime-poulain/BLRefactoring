using System.Net.Http.Json;

namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Changing the language, through the BFF.
/// </summary>
/// <remarks>
/// A sibling of <see cref="BffSessionClient"/> rather than a method on it: that client is the
/// session's — signing in and out — and a language is a preference the visitor holds with or
/// without one. The endpoint answers a cookie; the caller reloads the page so both runtimes
/// reboot in the chosen language (ADR 0088).
/// </remarks>
public sealed class BffCultureClient(IHttpClientFactory httpClientFactory) : IBffCultureClient
{
    /// <summary>
    /// Asks the BFF to record the choice in the standard culture cookie.
    /// </summary>
    public async Task ChangeLanguageAsync(string language, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Bff);

        var response = await client.PostAsJsonAsync("bff/culture", new CultureChoice(language), cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}

/// <summary>Changing the language, as the selector sees it.</summary>
public interface IBffCultureClient
{
    /// <summary>
    /// Records the language for every request after this one. The caller still owns the reload
    /// that makes the current page follow.
    /// </summary>
    /// <param name="language">A language code from the supported set — <c>fr</c>, not a locale.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task ChangeLanguageAsync(string language, CancellationToken cancellationToken = default);
}
