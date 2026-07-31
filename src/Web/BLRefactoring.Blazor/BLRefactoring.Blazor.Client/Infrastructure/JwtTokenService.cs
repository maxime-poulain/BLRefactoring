using Microsoft.JSInterop;

namespace BLRefactoring.Blazor.Client.Infrastructure;

/// <summary>
/// Reads and writes the JWT in the browser's localStorage.
/// </summary>
/// <remarks>
/// <para>
/// The browser's localStorage API is called straight through <see cref="IJSRuntime"/>. There is
/// no official framework abstraction that fits: <c>ProtectedLocalStorage</c> is the only one
/// Microsoft ships, and it builds on ASP.NET Core Data Protection — a server-side technology
/// that has no equivalent in WebAssembly, where the encryption key would have to travel to the
/// browser and protect nothing. So for a WebAssembly application, interop is the documented
/// approach, and three one-line calls do not warrant a dependency.
/// </para>
/// <para>
/// The token is stored as the raw string it already is. Wrapping it in JSON, as the previous
/// library did, only added quotes around a value that needed none.
/// </para>
/// <para>
/// localStorage is a browser-only API, so every member here needs JavaScript interop and can
/// only run once the application is interactive. That is why prerendering is off — see the
/// comment in App.razor. No guard is needed here for the same reason.
/// </para>
/// </remarks>
public class JwtTokenService(IJSRuntime jsRuntime) : IJwtTokenService
{
    private const string TokenKey = "auth_token";

    public async Task<string?> GetTokenAsync()
    {
        // getItem yields null for an absent key, which maps to the anonymous case.
        return await jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
    }

    public async Task SetTokenAsync(string token)
    {
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
    }

    public async Task RemoveTokenAsync()
    {
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
    }
}

public interface IJwtTokenService
{
    /// <summary>
    /// The stored token, or <c>null</c> when there is none — which is simply the anonymous case.
    /// </summary>
    Task<string?> GetTokenAsync();

    Task SetTokenAsync(string token);

    Task RemoveTokenAsync();
}
