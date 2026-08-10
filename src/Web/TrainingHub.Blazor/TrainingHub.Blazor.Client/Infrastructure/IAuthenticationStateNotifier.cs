namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// What a page announces after signing in or out: the identity may have changed, go look.
/// </summary>
/// <remarks>
/// The abstraction exists because two implementations answer it. In the browser it is
/// <see cref="BffAuthenticationStateProvider"/>, which re-reads <c>/bff/user</c> and tells every
/// <c>AuthorizeView</c>; during prerendering it is the host's provider, for which the announcement
/// is a no-op — a prerender never signs anybody in or out, and a layout that injected the
/// browser's concrete provider could not render on the server at all (ADR 0072).
/// </remarks>
public interface IAuthenticationStateNotifier
{
    /// <summary>
    /// Re-reads the identity and tells the interface about it.
    /// </summary>
    void NotifyAuthenticationChanged();
}
