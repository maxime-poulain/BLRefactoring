using BLRefactoring.DDDWithCqrs.Api.Middlewares;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator;
using BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator.Behaviors;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Infrastructure.Extensions;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMediator(configuration =>
{
    configuration.Assemblies = [typeof(CreateTrainerCommand).Assembly, typeof(TrainerCreatedDomainEvent).Assembly];
    configuration.PipelineBehaviors = [typeof(ValidationPipelineBehavior<,>), typeof(NoTrackingDuringQueryExecutionBehavior<,>)];
    configuration.ServiceLifetime = ServiceLifetime.Transient;
});

builder.Services.AddTransient<ICommandDispatcher, MediatorCommandDispatcher>();
builder.Services.AddTransient<IQueryDispatcher, MediatorQueryDispatcher>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddValidatorsFromAssembly(typeof(CreateTrainerCommandValidator).Assembly);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<FluentValidationMiddleware>();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();
    await context.Database.EnsureCreatedAsync();
}

app.Run();
