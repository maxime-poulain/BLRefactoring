using Blazored.LocalStorage;

namespace BLRefactoring.Blazor.Client.Infrastructure;

public class JwtTokenService(ILocalStorageService localStorageService) : IJwtTokenService
{
    private const string TokenKey = "auth_token";
    private static string _token;

    public async Task<string> GetTokenAsync()
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
    Task<string> GetTokenAsync();
    Task SetTokenAsync(string token);
    Task RemoveTokenAsync();
}
