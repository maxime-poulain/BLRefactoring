using Blazored.LocalStorage;

namespace BLRefactoring.Blazor.Client.Infrastructure;

/// <summary>
/// Reads and writes the JWT in the browser's localStorage.
/// </summary>
/// <remarks>
/// localStorage is a browser-only API, so every member here goes through JavaScript interop
/// and can only run once the application is interactive. That is why prerendering is off —
/// see the comment in App.razor.
/// </remarks>
public class JwtTokenService(ILocalStorageService localStorageService) : IJwtTokenService
{
    private const string TokenKey = "auth_token";

    public async Task<string?> GetTokenAsync()
    {
        return await localStorageService.GetItemAsync<string>(TokenKey);
    }

    public async Task SetTokenAsync(string token)
    {
        await localStorageService.SetItemAsync(TokenKey, token);
    }

    public async Task RemoveTokenAsync()
    {
        await localStorageService.RemoveItemAsync(TokenKey);
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
