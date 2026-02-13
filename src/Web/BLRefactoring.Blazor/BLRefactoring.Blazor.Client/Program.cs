using BLRefactoring.Blazor.Client.Extensions;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();

builder.Services.AddDependencies(builder.Configuration);

await builder.Build().RunAsync();
