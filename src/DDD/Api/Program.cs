using BLRefactoring.DDD.Application.EventHandlers;
using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.DDD.Infrastructure;
using BLRefactoring.DDD.Infrastructure.Repositories;
using BLRefactoring.DDD.Infrastructure.Repositories.EfCore;
using BLRefactoring.DDD.Infrastructure.Repositories.EfCore.Interceptor;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common;
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

builder.Services.AddTransient<IEventPublisher, MediatRDomainEventPublisher>();

builder.Services.AddScoped<ITransactionManager, TransactionManager>();

builder.Services.AddMediatR(cfg
    => cfg.RegisterServicesFromAssemblyContaining<DeleteTrainingWhenTrainerDeletedEventHandler>());

builder.Services.AddDbContext<TrainingContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TrainingContext"))
        .AddInterceptors(new IsTransientMaterializationInterceptor()));

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
    await context.Database.EnsureDeletedAsync();
    await context.Database.EnsureCreatedAsync();
}

app.Run();
