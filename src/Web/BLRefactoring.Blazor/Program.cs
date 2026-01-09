using BLRefactoring.Blazor.Components;
using BLRefactoring.Blazor.Components.Pages;
using BLRefactoring.GeneratedClients;
using FluentValidation;
using MudBlazor.Services;

// using Morris.Blazor.Validation;

namespace BLRefactoring.Blazor;
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // builder.Services.AddFormValidation(config =>
        //     config
        //         .AddDataAnnotationsValidation()
        //         .AddFluentValidation(typeof(SomeValidator).Assembly)
        // );
        builder.Services.AddMudServices();

        builder.Services.AddHttpClient<ITrainerClient, TrainerClient>();

// Register FluentValidation validators as singletons for better performance

        builder.Services.AddValidatorsFromAssemblyContaining<Home.OrderModelFluentValidator>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}
