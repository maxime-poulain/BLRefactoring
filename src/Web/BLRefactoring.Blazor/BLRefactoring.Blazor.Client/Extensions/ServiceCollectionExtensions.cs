using BLRefactoring.Blazor.Client.Infrastructure;
using BLRefactoring.GeneratedClients;
using Microsoft.AspNetCore.Components.Authorization;

namespace BLRefactoring.Blazor.Client.Extensions;

public static class ServiceCollectionExtensions
{
    private const string HttpClientName = "Default";

    /// <summary>
    /// Configuration key holding the address of the REST API this front end talks to.
    /// </summary>
    private const string ApiBaseAddressKey = "Api:BaseAddress";

    public static IServiceCollection AddDependencies(this IServiceCollection services)
    {
        // IJSRuntime, which JwtTokenService uses to reach localStorage, is registered by the
        // Blazor host itself — there is nothing to add here for storage.
        return services
            .AddServices()
            .AddBLRefactoringBlazorClient();
    }

    public static IServiceCollection AddBLRefactoringBlazorClient(this IServiceCollection services)
    {
        // The address is read when a client is first created rather than at registration,
        // because this method also runs on the server host — which registers these services but
        // never resolves them, since no component renders server-side while prerendering is off.
        // The value therefore only has to exist in the configuration the browser downloads;
        // adding it to the host's appsettings would be adding a setting nothing reads.
        //
        // It lives in the environment-specific file rather than the shared one: a localhost
        // address is a development fact, and the same split already governs the API hosts,
        // whose connection string and JWT settings sit in appsettings.Development.json.
        services.AddHttpClient(HttpClientName, (serviceProvider, client) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            var baseAddress = configuration[ApiBaseAddressKey]
                ?? throw new InvalidOperationException(
                    $"Missing configuration value '{ApiBaseAddressKey}'. The WebAssembly client " +
                    "reads it from wwwroot/appsettings.{Environment}.json, downloaded at startup. " +
                    "Every environment must name its own API: there is no sensible default, and " +
                    "falling back to a localhost address in production would fail obscurely.");

            client.BaseAddress = new Uri(baseAddress);
        }).AddHttpMessageHandler<JwtTokenHandler>();

        services.AddHttpClient<ITrainerClient, TrainerClient>(HttpClientName);
        services.AddHttpClient<ITrainingClient, TrainingClient>(HttpClientName);
        services.AddHttpClient<IAuthClient, AuthClient>(HttpClientName);

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddAuthorizationCore();
        services.AddScoped<JwtAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
        services.AddTransient<IJwtTokenService, JwtTokenService>();
        services.AddTransient<JwtTokenHandler>();
        return services;
    }
}
