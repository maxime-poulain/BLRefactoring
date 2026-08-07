using System.Net.Http.Headers;
using System.Net.Http.Json;
using TrainingHub.Shared.Api.Contracts.Auth;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// Registration and sign-in over HTTP. Both stacks derive their auth controllers from the
/// same <c>AuthControllerBase</c>, so the routes and payloads are identical and this helper
/// serves either one.
/// </summary>
public static class AuthHelper
{
    private static int _counter;

    /// <summary>
    /// Create unique register request.
    /// </summary>
    /// <param name="firstname">The first name — a marker some test-only consumers key on, such as
    /// <see cref="FailOnceWhenTrainerCreatedIntegrationEventHandler.Marker"/>.</param>
    public static RegisterHttpRequest CreateUniqueRegisterRequest(string firstname = "Test")
    {
        var id = Interlocked.Increment(ref _counter);
        return new RegisterHttpRequest
        {
            Username = $"testuser{id}",
            Email = $"testuser{id}@example.com",
            Password = "pass",
            ConfirmPassword = "pass",
            Firstname = firstname,
            Lastname = $"User{id}"
        };
    }

    /// <summary>
    /// Register async.
    /// </summary>
    public static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, RegisterHttpRequest request)
    {
        return await client.PostAsJsonAsync("/Auth/register", request);
    }

    /// <summary>
    /// Login async.
    /// </summary>
    public static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/Auth/login", new LoginHttpRequest
        {
            Username = username,
            Password = password
        });

        response.EnsureSuccessStatusCode();
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginHttpResponse>();
        return loginResponse!.Token;
    }

    /// <remarks>
    /// Takes the capability rather than the fixture. <c>WebApplicationFactory&lt;TEntryPoint&gt;</c>
    /// would drag its entry-point type parameter into every shared test that signs a caller in,
    /// and the shared tests are generic over the fixture precisely so they do not have to name a
    /// host. Both concrete fixtures satisfy <see cref="IHttpClientSource"/>, so no call site
    /// changed.
    /// </remarks>
    public static async Task<HttpClient> RegisterAndGetAuthenticatedClientAsync(IHttpClientSource factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var client = factory.CreateClient();
        var request = CreateUniqueRegisterRequest();

        var registerResponse = await RegisterAsync(client, request);
        registerResponse.EnsureSuccessStatusCode();

        var token = await LoginAsync(client, request.Username, request.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>
    /// Registers an account, then signs in with the wrong password, and answers what came back.
    /// </summary>
    /// <remarks>
    /// The arrangement rather than the assertion, because two tests need it and neither is about
    /// it: one holds the status to 401, the other holds the body to the problem shape ADR 0004
    /// requires. Registering first is what makes the refusal about the password — an unknown
    /// username is refused too, identically and on purpose, so a test that skipped the registration
    /// would pass without proving anything about credentials.
    /// </remarks>
    /// <param name="factory">The suite's fixture.</param>
    /// <returns>The response to the refused sign-in.</returns>
    public static async Task<HttpResponseMessage> SignInWithTheWrongPasswordAsync(IHttpClientSource factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var client = factory.CreateClient();
        var request = CreateUniqueRegisterRequest();
        (await RegisterAsync(client, request)).EnsureSuccessStatusCode();

        return await client.PostAsJsonAsync("/Auth/login", new LoginHttpRequest
        {
            Username = request.Username,
            Password = "wrong_password"
        });
    }
}
