using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.DDD.Application.Services.TrainerServices.Dto;
using BLRefactoring.DDD.Application.Services.TrainingServices.Dtos;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace BLRefactoring.DDD.Application.Services;

public static class Mappers
{
    public static List<Rate> ToRates(this List<RateDto> rates)
    {
        return rates.ConvertAll(rateDto => Rate.Create(
            rateDto.Value,
            Comment.Create(rateDto.Comment).Value,
            rateDto.AuthorId).Value);
    }

    public static TrainingDto ToDto(this Training training)
    {
        return new TrainingDto
        {
            Id = training.Id,
            Title = training.Title,
            Rates = training.Rates.Select(rate => new RateDto
            {
                Value = rate.Value,
                Comment = rate.Comment.Content,
                AuthorId = rate.AuthorId
            }).ToList(),
            StartDate = training.StartDate,
            EndDate = training.EndDate,
            TrainerId = training.TrainerId
        };
    }

    public static List<TrainingDto> ToDtos(this IEnumerable<Training> trainings)
    {
        return trainings.Select(training => new TrainingDto
        {
            Id = training.Id,
            Title = training.Title,
            Rates = training.Rates.Select(rate => new RateDto
            {
                Value = rate.Value,
                Comment = rate.Comment.Content,
                AuthorId = rate.AuthorId
            }).ToList(),
            StartDate = training.StartDate,
            EndDate = training.EndDate,
            TrainerId = training.TrainerId
        }).ToList();
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
