using MudBlazor.Services;
using BLRefactoring.Blazor.Components;
using BLRefactoring.Blazor.Components.Pages;
using BLRefactoring.Blazor.Services;
using BLRefactoring.GeneratedClients;
using FluentValidation;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

builder.Services.AddHttpContextAccessor();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var services = builder.Services;

services.AddTransient<AuthorizationMessageHandler>();
var defaultClientName = "BLRefactoringApi";
services.AddHttpClient(defaultClientName, client => { client.BaseAddress = new Uri("https://localhost:7249"); })
    .AddHttpMessageHandler<AuthorizationMessageHandler>();

services.AddHttpClient<ITrainerClient, TrainerClient>(defaultClientName);
services.AddHttpClient<ITrainingClient, TrainingClient>(defaultClientName);
services.AddHttpClient<IAuthClient, AuthClient>(defaultClientName);

services.AddTransient<ITokenService, TokenService>();
services.AddScoped<JwtAuthenticationStateProvider>();
services.AddScoped<AuthenticationStateProvider>(provider
    => provider.GetRequiredService<JwtAuthenticationStateProvider>());

services.AddValidatorsFromAssemblyContaining<TrainerProfile.TrainerProfileValidator>();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
