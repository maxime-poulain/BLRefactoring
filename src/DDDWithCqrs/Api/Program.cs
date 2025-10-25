using BLRefactoring.DDDWithCqrs.Api.Middlewares;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.MediatR;
using BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.MediatR.Behaviors;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.DDD.Infrastructure;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore.Interceptor;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

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

builder.Services.AddTransient<ITrainingRepository, TrainingRepository>();
builder.Services.AddTransient<IUniquenessTitleChecker, TrainingRepository>();
builder.Services.AddTransient<ITrainerRepository, TrainerRepository>();

builder.Services.AddTransient<IEventPublisher, MediatRDomainEventPublisher>();

builder.Services.AddScoped<ITransactionManager, TransactionManager>();

builder.Services.AddDbContext<TrainingContext>(options =>
    options.UseSqlite($@"Data Source=c:\temp\cqrsDDD-{Guid.NewGuid()}.db")
        .AddInterceptors(new IsTransientMaterializationInterceptor()));

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
    context.Database.OpenConnection();
    //await context.Database.EnsureDeletedAsync();
    await context.Database.EnsureCreatedAsync();
}

app.Run();
