using System.Globalization;
using TrainingHub.Blazor.Client.Extensions;
using TrainingHub.Translations;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();

builder.Services.AddDependencies();

var host = builder.Build();

// The language the server already answered in, read back rather than resolved a second time: the
// server stamped its resolution on <html lang> — it renders that element on every route, whether
// or not the route prerenders — and the two runtimes agreeing on one source is what makes the
// first interactive frame speak the same language as the first painted one (ADR 0088). The
// framework loaded the globalization data and every language's satellite assembly at boot, so
// setting the culture is all that remains. Anything unexpected in the attribute falls back to the
// default rather than throwing before the application exists.
var language = await host.Services.GetRequiredService<IJSRuntime>()
    .InvokeAsync<string?>("document.documentElement.getAttribute", "lang");

var culture = new CultureInfo(
    language is not null && SupportedLanguages.All.Contains(language, StringComparer.OrdinalIgnoreCase)
        ? language
        : SupportedLanguages.Default);

CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();
