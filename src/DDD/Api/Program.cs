using BLRefactoring.DDD.Application.EventHandlers;
using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.DDD.Infrastructure;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore.Interceptor;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddTransient<ITrainingApplicationService, TrainingApplicationService>();
builder.Services.AddTransient<ITrainingRepository, TrainingRepository>();
builder.Services.AddTransient<IUniquenessTitleChecker, TrainingRepository>();
builder.Services.AddTransient<ITrainerRepository, TrainerRepository>();
builder.Services.AddTransient<ITrainerApplicationService, TrainerApplicationService>();

builder.Services.AddTransient<IEventPublisher, MediatorRDomainEventPublisher>();

builder.Services.AddScoped<ITransactionManager, TransactionManager>();

builder.Services
    .AddSingleton<IsTransientSaveChangesInterceptor>()
    .AddSingleton<IsTransientMaterializationInterceptor>();

builder.Services.AddDbContext<TrainingContext>((serviceProvider, options) =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TrainingContext"))
        .AddInterceptors(
            serviceProvider.GetRequiredService<IsTransientSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<IsTransientMaterializationInterceptor>())
    );

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();
    await context.Database.EnsureCreatedAsync();
}

app.Run();
