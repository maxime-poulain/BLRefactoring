using BLRefactoring.DDDWithCqrs.Api.Middlewares;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator;
using BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator.Behaviors;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Infrastructure;
using BLRefactoring.Shared.Infrastructure.Repositories;
using BLRefactoring.Shared.Infrastructure.Repositories.EfCore;
using BLRefactoring.Shared.Infrastructure.Repositories.EfCore.Interceptor;
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
    configuration.ServiceLifetime = ServiceLifetime.Scoped;
});

builder.Services.AddTransient<ICommandDispatcher, MediatorCommandDispatcher>();
builder.Services.AddTransient<IQueryDispatcher, MediatorQueryDispatcher>();

builder.Services.AddTransient<ITrainingRepository, TrainingRepository>();
builder.Services.AddTransient<IUniquenessTitleChecker, TrainingRepository>();
builder.Services.AddTransient<ITrainerRepository, TrainerRepository>();

builder.Services.AddTransient<IEventPublisher, MediatorRDomainEventPublisher>();

builder.Services.AddScoped<ITransactionManager, TransactionManager>();

builder.Services
    .AddSingleton<IsTransientSaveChangesInterceptor>()
    .AddSingleton<IsTransientMaterializationInterceptor>();

builder.Services.AddDbContext<TrainingContext>((serviceProvider, options) =>
    options.UseSqlite($@"Data Source=Application.db;Cache=Shared")
        .AddInterceptors(
            serviceProvider.GetRequiredService<IsTransientSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<IsTransientMaterializationInterceptor>())
    );

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
    await context.Database.OpenConnectionAsync();
    //await context.Database.EnsureDeletedAsync();
    await context.Database.EnsureCreatedAsync();
}

app.Run();
