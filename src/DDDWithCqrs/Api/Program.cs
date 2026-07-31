using BLRefactoring.DDDWithCqrs.Api.Middlewares;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using BLRefactoring.Shared.Application.EventHandlers;
using BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator;
using BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator.Behaviors;
using BLRefactoring.Shared.Api.Extensions;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Infrastructure.Extensions;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Shared with the layered host so the two cannot drift: a policy defined in one Program.cs only
// protects that host. See CorsExtensions for why the origins come from configuration.
builder.Services.AddApiCors(builder.Configuration);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMediator(configuration =>
{
    configuration.Assemblies =
    [
        typeof(CreateTrainerCommand).Assembly,      // DDDWithCqrs.Application: commands + command handlers
        typeof(TrainerCreatedDomainEvent).Assembly, // Shared.Domain: domain events
        typeof(MediatorCommandDispatcher).Assembly, // DDDWithCqrs.Infrastructure: query handlers
        typeof(SendWelcomeEmailWhenTrainerCreatedEventHandler).Assembly // Shared.Application: domain event handlers
    ];
    configuration.PipelineBehaviors = [typeof(ValidationPipelineBehavior<,>), typeof(NoTrackingDuringQueryExecutionBehavior<,>)];
    configuration.ServiceLifetime = ServiceLifetime.Transient;
});

builder.Services.AddTransient<ICommandDispatcher, MediatorCommandDispatcher>();
builder.Services.AddTransient<IQueryDispatcher, MediatorQueryDispatcher>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddValidatorsFromAssembly(typeof(CreateTrainerCommandValidator).Assembly);

// Identity, JWT validation and the ownership policy are the same on both hosts, and are declared
// once in BLRefactoring.Shared.Api so neither can quietly lose a rule the other keeps.
builder.Services.AddApiIdentity(builder.Configuration);
builder.Services.AddJwtBearerAuthentication(builder.Configuration);
builder.Services.AddTrainingOwnerAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.

// The exception handlers come first, so that everything downstream is covered: authentication,
// authorization, routing and the actions alike. Registered after the authentication middlewares —
// where they used to sit — they let anything thrown while authenticating escape to the host.
// The global handler stays outside the validation one, which turns a ValidationException into a
// 400 before the global handler would turn it into a 500.
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<FluentValidationMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Before authentication on purpose: an unauthenticated cross-origin call must still be answered
// with the CORS headers, or the browser reports a CORS failure instead of the 401 it received.
app.UseApiCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.MigrateDatabasesAsync();

app.Run();

public partial class Program { }
