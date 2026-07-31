using BLRefactoring.Blazor.Client.Extensions;
using MudBlazor.Services;
using BLRefactoring.Blazor.Components;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

builder.Services.AddDependencies(builder.Configuration);

// This host authenticates nobody. It serves the WebAssembly application and its static
// assets; the browser then talks to the API directly, carrying the JWT it holds in
// localStorage. Authorization is decided client-side by AuthorizeRouteView, and the API
// enforces it for real on every request.
//
// A cookie authentication scheme used to be registered here, together with a default policy
// that authorized everything. Nothing consumed either of them, and together they suggested a
// server-side authentication model this application does not have.

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(BLRefactoring.Blazor.Client._Imports).Assembly);

app.Run();
