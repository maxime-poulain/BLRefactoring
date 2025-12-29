using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.DDD.Application.Services.TrainerServices.Dto;
using BLRefactoring.DDD.Application.Services.TrainingServices.Dtos;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace BLRefactoring.DDD.Application.Services;

public static class Mappers
{
    public static TrainingDto ToDto(this Training training)
    {
        return new TrainingDto
        {
            Id = training.Id,
            Title = training.Title,
            StartDate = training.StartDate,
            EndDate = training.EndDate,
            TrainerId = training.TrainerId
        };
    }

    public static List<TrainingDto> ToDtos(this IEnumerable<Training> trainings)
    {
        return trainings.Select(ToDto).ToList();
    }

    public static TrainerDto ToDto(this Trainer trainer)
    {
        return new TrainerDto()
        {
            Id = trainer.Id,
            Email = trainer.Email.FullAddress,
            Firstname = trainer.Name.Firstname,
            Lastname = trainer.Name.Lastname
        };
    }
}
