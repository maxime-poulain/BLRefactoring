using Microsoft.AspNetCore.Components.Authorization;
using TrainingHub.Blazor.Client.Infrastructure;

namespace TrainingHub.Blazor.Prerendering;

/// <summary>
/// Who the caller is during a prerender: whatever the cookie already said.
/// </summary>
/// <remarks>
/// The browser's provider asks <c>/bff/user</c> over HTTP, which on this side of the wire would be
/// the host calling itself mid-request. There is no need to: by the time a component renders, the
/// BFF's own pipeline has authenticated the cookie and <see cref="HttpContext.User"/> is the same
/// principal that endpoint would describe (ADR 0009, ADR 0072).
/// <para>
/// The notifier half is a no-op on purpose: a prerender never signs anybody in or out, so there is
/// nothing to announce and nobody left to hear it — the page that acts is the interactive one.
/// </para>
/// </remarks>
public sealed class HostAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    : AuthenticationStateProvider, IAuthenticationStateNotifier
{
    /// <summary>
    /// Answers the principal the BFF's pipeline already authenticated.
    /// </summary>
    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(
            httpContextAccessor.HttpContext?.User ?? new System.Security.Claims.ClaimsPrincipal()));

    /// <summary>
    /// Does nothing: a prerender never changes who is signed in.
    /// </summary>
    public void NotifyAuthenticationChanged()
    {
    }
}
