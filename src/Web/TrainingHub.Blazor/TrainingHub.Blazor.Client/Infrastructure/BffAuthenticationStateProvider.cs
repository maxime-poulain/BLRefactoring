using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Asks the BFF who the user is, instead of decoding a token the browser holds.
/// </summary>
/// <remarks>
/// The previous provider read a JWT out of <c>localStorage</c> and parsed its claims in the page.
/// There is no token here any more: identity is whatever <c>/bff/user</c> reports from the cookie,
/// and the browser cannot see, forge or exfiltrate it. See ADR 0009.
/// <para>
/// The answer is cached for the lifetime of the provider, which is the lifetime of the
/// application: without that, every <c>AuthorizeView</c> in a page would issue its own request.
/// The cache is dropped whenever sign-in or sign-out is announced.
/// </para>
/// </remarks>
public sealed class BffAuthenticationStateProvider(IHttpClientFactory httpClientFactory)
    : AuthenticationStateProvider, IAuthenticationStateNotifier
{
    private const string UserEndpoint = "bff/user";

    private Task<AuthenticationState>? _state;

    /// <summary>
    /// Answers who is signed in, asking the BFF rather than reading a token the browser holds.
    /// </summary>
    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        _state ??= FetchAsync();

    /// <summary>
    /// Re-reads the identity from the BFF and tells the UI about it.
    /// </summary>
    /// <remarks>
    /// Used after signing in or out. It deliberately re-fetches rather than trusting what the
    /// caller believes happened: the cookie is the truth, and this host is the only thing that can
    /// read it.
    /// </remarks>
    public void NotifyAuthenticationChanged()
    {
        _state = FetchAsync();
        NotifyAuthenticationStateChanged(_state);
    }

    private async Task<AuthenticationState> FetchAsync()
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.Bff);

        try
        {
            var user = await client.GetFromJsonAsync<BffUser>(UserEndpoint);

            if (user?.Claims is not { Count: > 0 })
            {
                return Anonymous;
            }

            var identity = new ClaimsIdentity(
                user.Claims.Select(claim => new Claim(claim.Type, claim.Value)),
                authenticationType: "bff");

            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (HttpRequestException)
        {
            // The BFF being unreachable is not an identity: treating it as anonymous shows the
            // signed-out interface rather than leaving the application in a half-rendered state.
            return Anonymous;
        }
    }

    private static AuthenticationState Anonymous => new(new ClaimsPrincipal(new ClaimsIdentity()));
}
