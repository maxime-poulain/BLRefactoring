using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.DDD.Application.Services.TrainingServices.Dtos;
using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace BLRefactoring.DDD.Application.Services.TrainingServices;

public static class Mappers
{
    public static List<Rate> ToRates(this List<RateDto> rates)
    {
        return rates.ConvertAll(r => Rate.Create(
            r.Value,
            Comment.Create(r.Comment).Value,
            r.AuthorId).Value);
    }

    public static TrainingDto ToDto(this Training training)
    {
        return new TrainingDto
        {
            Id = training.Id,
            Title = training.Title,
            Rates = training.Rates.ConvertAll(r => new RateDto
            {
                Value = r.Value,
                Comment = r.Comment.Content,
                AuthorId = r.AuthorId
            }),
            StartDate = training.StartDate,
            EndDate = training.EndDate,
            TrainerId = training.TrainerIdd
        };
    }

    public static List<TrainingDto> ToDtos(this IEnumerable<Training> trainings)
    {
        return trainings.Select(training => new TrainingDto
        {
            Id = training.Id,
            Title = training.Title,
            Rates = training.Rates.ConvertAll(r => new RateDto
            {
                Value = r.Value, Comment = r.Comment.Content, AuthorId = r.AuthorId
            }),
            StartDate = training.StartDate,
            EndDate = training.EndDate,
            TrainerId = training.TrainerIdd
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
