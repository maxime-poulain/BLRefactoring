using System.Net.Http.Headers;
using System.Net.Http.Json;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BLRefactoring.Api.TestKit;

/// <summary>
/// Registration and sign-in over HTTP. Both stacks derive their auth controllers from the
/// same <c>AuthControllerBase</c>, so the routes and payloads are identical and this helper
/// serves either one.
/// </summary>
public static class AuthHelper
{
    private static int _counter;

    public static RegisterRequest CreateUniqueRegisterRequest()
    {
        var id = Interlocked.Increment(ref _counter);
        return new RegisterRequest
        {
            Username = $"testuser{id}",
            Email = $"testuser{id}@example.com",
            Password = "pass",
            ConfirmPassword = "pass",
            Firstname = "Test",
            Lastname = $"User{id}"
        };
    }

    public static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, RegisterRequest request)
    {
        return await client.PostAsJsonAsync("/Auth/register", request);
    }

    public static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/Auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        response.EnsureSuccessStatusCode();
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return loginResponse!.Token;
    }

    public static async Task<HttpClient> RegisterAndGetAuthenticatedClientAsync<TEntryPoint>(
        WebApplicationFactory<TEntryPoint> factory)
        where TEntryPoint : class
    {
        var client = factory.CreateClient();
        var request = CreateUniqueRegisterRequest();

        var registerResponse = await RegisterAsync(client, request);
        registerResponse.EnsureSuccessStatusCode();

        var token = await LoginAsync(client, request.Username, request.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
