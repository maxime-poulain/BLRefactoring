using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace BLRefactoring.Blazor.Services;

public interface ITokenService
{
    ValueTask SetTokenAsync(string token);
    ValueTask<string?> GetTokenAsync();
    ValueTask DeleteTokenAsync();
}

public class TokenService(ProtectedSessionStorage sessionStorage) : ITokenService
{
    private const string TokenKey = "auth_token";

    private static string Token {get;set;}

    public ValueTask SetTokenAsync(string token)
    {
        Token = token;
        return sessionStorage.SetAsync(TokenKey, token);
    }

    public async ValueTask<string?> GetTokenAsync()
    {
        try
        {
            var result = await sessionStorage.GetAsync<string>(TokenKey);
            return result.Success ? result.Value : null;
        }
        catch (InvalidOperationException)
        {
            return !string.IsNullOrWhiteSpace(Token) ? Token : null;
        }
    }

    public ValueTask DeleteTokenAsync() => sessionStorage.DeleteAsync(TokenKey);
}
