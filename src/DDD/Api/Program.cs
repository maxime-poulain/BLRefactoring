using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.Shared.Api.Extensions;
using BLRefactoring.Shared.Application.EventHandlers;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Shared with the CQRS host so the two cannot drift: a policy defined in one Program.cs only
// protects that host. See CorsExtensions for why the origins come from configuration.
builder.Services.AddApiCors(builder.Configuration);

// One error format for both hosts: RFC 7807 ProblemDetails, whatever failed.
builder.Services.AddApiProblemDetails();

// The framework's OpenAPI generator, shared with the CQRS host — which described the same API with
// a different library, and without a security scheme at all. See ADR 0006.
builder.Services.AddApiOpenApi();

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

// First, so everything downstream is covered: authentication, authorization, routing and the
// actions alike. Declared once in Shared.Api and shared with the CQRS host.
app.UseApiExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseApiOpenApi();
}

app.UseHttpsRedirection();

app.UseApiCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.EnsureDatabasesAreUpToDateAsync();

app.Run();

public partial class Program { }
