using System.Net;
using System.Net.Http.Json;
using TrainingHub.GeneratedClients;

namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Signing in and out, through the BFF.
/// </summary>
/// <remarks>
/// The generated client is not used for this, and cannot be: the API answers <c>/Auth/login</c>
/// with the token, and a generated method returning it would put the credential straight back in
/// the browser — the thing ADR 0009 exists to prevent. The BFF's own endpoint answers with a cookie
/// and no body.
/// </remarks>
public sealed class BffSessionClient(IHttpClientFactory httpClientFactory) : IBffSessionClient
{
    /// <summary>
    /// Signs in through the BFF, which keeps the token server-side and answers with a cookie.
    /// </summary>
    public async Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Bff);

        var response = await client.PostAsJsonAsync(
            "bff/login",
            new LoginHttpRequest { Username = username, Password = password },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        return true;
    }

    /// <summary>
    /// Creates an account through the BFF, whose endpoint is the one a visitor can reach.
    /// </summary>
    /// <remarks>
    /// The generated client points at <c>/api/Auth/register</c>, which the proxy's catch-all route
    /// answers with a 401 for anyone without a session — and a visitor creating an account never
    /// has one. The BFF forwards the call itself and passes the API's problem document through,
    /// because the per-field messages inside it are what the form renders.
    /// </remarks>
    public async Task<ProblemDetails?> RegisterAsync(
        RegisterHttpRequest registration,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Bff);

        var response = await client.PostAsJsonAsync("bff/register", registration, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        if (response.Content.Headers.ContentType?.MediaType?.EndsWith("json", StringComparison.Ordinal) == true)
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ProblemDetails>(cancellationToken);

            if (problem is not null)
            {
                return problem;
            }
        }

        // A refusal with no readable document — a gateway's HTML page, an empty body — is an
        // outage to report, not a validation verdict to render.
        throw new HttpRequestException(
            $"Registration answered {(int)response.StatusCode} without a problem document.");
    }

    /// <summary>
    /// Ends the session.
    /// </summary>
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Bff);

        var response = await client.PostAsync("bff/logout", content: null, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            // The session had already expired. Signing out of nothing succeeded: the caller wants
            // to end up signed out, and they are.
            return;
        }

        response.EnsureSuccessStatusCode();
    }
}

/// <summary>Signing in and out, as the pages see it.</summary>
public interface IBffSessionClient
{
    /// <summary>
    /// Signs in. <see langword="false"/> means the credentials were refused — an ordinary outcome,
    /// not an exception.
    /// </summary>
    Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an account. <see langword="null"/> means it was created; a problem document is the
    /// API's refusal, carrying the per-field messages the form renders.
    /// </summary>
    /// <param name="registration">The account to create.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<ProblemDetails?> RegisterAsync(
        RegisterHttpRequest registration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the session.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
