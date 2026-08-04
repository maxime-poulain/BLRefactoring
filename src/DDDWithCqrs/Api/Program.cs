using TrainingHub.DDDWithCqrs.Application.Features.Trainers.Create;
using TrainingHub.Shared.Application.EventHandlers;
using TrainingHub.DDDWithCqrs.Infrastructure.ThirdParty.Mediator;
using TrainingHub.DDDWithCqrs.Infrastructure.ThirdParty.Mediator.Behaviors;
using TrainingHub.Shared.Api.Extensions;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using TrainingHub.Shared.Infrastructure.Extensions;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Shared with the layered host so the two cannot drift: a policy defined in one Program.cs only
// protects that host. See CorsExtensions for why the origins come from configuration.
builder.Services.AddApiCors(builder.Configuration);

// One error format for both hosts: RFC 7807 ProblemDetails, whatever failed.
builder.Services.AddApiProblemDetails();

// The framework's OpenAPI generator, shared with the layered host. This one used to call a bare
// AddSwaggerGen(), so its document declared no security scheme and no authenticated endpoint could
// be tried from its UI. See ADR 0006.
builder.Services.AddApiOpenApi();

builder.Services.AddMediator(configuration =>
{
    configuration.Assemblies =
    [
        typeof(CreateTrainerCommand).Assembly,      // DDDWithCqrs.Application: commands + command handlers
        typeof(TrainerCreatedDomainEvent).Assembly, // Shared.Domain: domain events
        typeof(MediatorCommandDispatcher).Assembly, // DDDWithCqrs.Infrastructure: query handlers
        typeof(PublishIntegrationEventWhenTrainerCreatedEventHandler).Assembly // Shared.Application: domain event handlers
    ];
    configuration.PipelineBehaviors = [typeof(ValidationPipelineBehavior<,>), typeof(NoTrackingDuringQueryExecutionBehavior<,>)];
    configuration.ServiceLifetime = ServiceLifetime.Transient;
});

builder.Services.AddTransient<ICommandDispatcher, MediatorCommandDispatcher>();
builder.Services.AddTransient<IQueryDispatcher, MediatorQueryDispatcher>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddValidatorsFromAssembly(typeof(CreateTrainerCommandValidator).Assembly);

// Identity, JWT validation and the ownership policy are the same on both hosts, and are declared
// once in TrainingHub.Shared.Api so neither can quietly lose a rule the other keeps.
builder.Services.AddApiIdentity(builder.Configuration);
builder.Services.AddJwtBearerAuthentication(builder.Configuration);
builder.Services.AddTrainingOwnerAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.

// First, so everything downstream is covered: authentication, authorization, routing and the
// actions alike. The handlers themselves, and the order they are tried in, are declared once in
// Shared.Api and shared with the layered host — which had no exception handling at all.
app.UseApiExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseApiOpenApi();
}

app.UseHttpsRedirection();

// Before authentication on purpose: an unauthenticated cross-origin call must still be answered
// with the CORS headers, or the browser reports a CORS failure instead of the 401 it received.
app.UseApiCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.EnsureDatabasesAreUpToDateAsync();

app.Run();

/// <summary>
/// The entry point of the CQRS host.
/// </summary>
/// <remarks>
/// Declared explicitly, rather than left to the compiler, so the integration tests can
/// name it as the type argument to <c>WebApplicationFactory</c>.
/// </remarks>
public sealed partial class Program { }
