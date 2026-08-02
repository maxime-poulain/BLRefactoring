using System.Net;
using System.Net.Http.Json;
using BLRefactoring.GeneratedClients;

namespace BLRefactoring.Blazor.Client.Infrastructure;

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
    public async Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Bff);

        var response = await client.PostAsJsonAsync(
            "bff/login",
            new LoginRequest { Username = username, Password = password },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        return true;
    }

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

    Task LogoutAsync(CancellationToken cancellationToken = default);
}
