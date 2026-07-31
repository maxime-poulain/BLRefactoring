using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.Shared.Api.Extensions;
using BLRefactoring.Shared.Application.EventHandlers;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Shared with the CQRS host so the two cannot drift: a policy defined in one Program.cs only
// protects that host. See CorsExtensions for why the origins come from configuration.
builder.Services.AddApiCors(builder.Configuration);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(options =>
{
    options.AddSecurity(JwtBearerDefaults.AuthenticationScheme, new NSwag.OpenApiSecurityScheme
    {
        Type = NSwag.OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your token JWT"
    });

    options.OperationProcessors.Add(new NSwag.Generation.Processors.Security.AspNetCoreOperationSecurityScopeProcessor("Bearer"));
});

builder.Services.AddTransient<ITrainingApplicationService, TrainingApplicationService>();
builder.Services.AddTransient<ITrainerApplicationService, TrainerApplicationService>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Transient;
    options.Assemblies = [typeof(TrainerDeletedDomainEvent).Assembly, typeof(DeleteTrainingWhenTrainerDeletedEventHandler).Assembly];
});

// Identity, JWT validation and the ownership policy are the same on both hosts, and are declared
// once in BLRefactoring.Shared.Api so neither can quietly lose a rule the other keeps.
builder.Services.AddApiIdentity(builder.Configuration);
builder.Services.AddJwtBearerAuthentication(builder.Configuration);
builder.Services.AddTrainingOwnerAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseApiCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.MigrateDatabasesAsync();

app.Run();

public partial class Program { }
