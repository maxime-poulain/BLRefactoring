using BLRefactoring.DDD.Application.EventHandlers;
using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Infrastructure;
using BLRefactoring.Shared.Infrastructure.Extensions;
using BLRefactoring.Shared.Infrastructure.Repositories;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Interceptor;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddTransient<ITrainingApplicationService, TrainingApplicationService>();
builder.Services.AddTransient<IUniquenessTitleChecker, TrainingRepository>();
builder.Services.AddTransient<ITrainerApplicationService, TrainerApplicationService>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddSingleton<IsTransientSaveChangesInterceptor>()
    .AddSingleton<IsTransientMaterializationInterceptor>()
    .AddSingleton<DomainEventInterceptor>();

builder.Services.AddDbContext<TrainingContext>((serviceProvider, options) =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TrainingContext"))
        .AddInterceptors(
            serviceProvider.GetRequiredService<IsTransientSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<IsTransientMaterializationInterceptor>(),
            serviceProvider.GetRequiredService<DomainEventInterceptor>())
    );

builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies = [typeof(TrainerDeletedDomainEvent).Assembly, typeof(DeleteTrainingWhenTrainerDeletedEventHandler).Assembly];
});

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
