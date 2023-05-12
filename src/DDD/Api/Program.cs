using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.DDD.Infrastructure.Repositories;

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

app.Run();
