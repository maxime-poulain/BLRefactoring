using BLRefactoring.Blazor.Bff;
using MudBlazor.Services;
using BLRefactoring.Blazor.Components;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// The browser's services are deliberately NOT registered here. AddDependencies describes the
// WebAssembly application's world — an HttpClient pointed at the page's own origin, an identity
// read from /bff/user — and this host is the other side of both. Prerendering is off (see
// App.razor), so nothing renders server-side and nothing would resolve them; what registering them
// did do was collide with this host's own API client, which shares the name "Api", and answer
// sign-in with a 500. The BFF suite catches that.

// This host is the backend for frontend. It terminates authentication, keeps the API's access
// token in a cookie the browser cannot read, and forwards everything under /api to the API with
// that token attached. The WebAssembly application therefore talks only to its own origin and
// never holds a credential.
//
// It used to authenticate nobody: the browser kept a JWT in localStorage and called the API
// cross-origin, which put a portable credential within reach of any script running in the page.
// See ADR 0009.
builder.Services.AddBff(builder.Configuration);

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

// Before UseAntiforgery, because it is what authenticates the request: antiforgery binds its
// token to the signed-in user, and a pipeline that validated before knowing who the caller is
// would be validating against nobody.
app.UseBff();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(BLRefactoring.Blazor.Client._Imports).Assembly);

app.Run();

/// <summary>
/// Named so the BFF suite can host this application. Top-level statements compile to an internal
/// <c>Program</c>, which <c>WebApplicationFactory&lt;T&gt;</c> cannot reach.
/// </summary>
public partial class Program { }
