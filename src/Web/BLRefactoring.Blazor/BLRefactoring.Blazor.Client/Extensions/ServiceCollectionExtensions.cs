using BLRefactoring.Blazor.Client.Infrastructure;
using BLRefactoring.GeneratedClients;
using Microsoft.AspNetCore.Components.Authorization;

namespace BLRefactoring.Blazor.Client.Extensions;

public static class ServiceCollectionExtensions
{
    private const string HttpClientName = "Default";

    public static IServiceCollection AddDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        // IJSRuntime, which JwtTokenService uses to reach localStorage, is registered by the
        // Blazor host itself — there is nothing to add here for storage.
        return services
            .AddServices(configuration)
            .AddBLRefactoringBlazorClient(configuration);
    }

    public static IServiceCollection AddBLRefactoringBlazorClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient(HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://localhost:7249");
        }).AddHttpMessageHandler<JwtTokenHandler>();

        services.AddHttpClient<ITrainerClient, TrainerClient>(HttpClientName);
        services.AddHttpClient<ITrainingClient, TrainingClient>(HttpClientName);
        services.AddHttpClient<IAuthClient, AuthClient>(HttpClientName);

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthorizationCore();
        services.AddScoped<JwtAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
        services.AddTransient<IJwtTokenService, JwtTokenService>();
        services.AddTransient<JwtTokenHandler>();
        return services;
    }
}
