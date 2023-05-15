using BLRefactoring.DDDWithCqrs.Application.Features.Trainings;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace BLRefactoring.DDDWithCqrs.Application.Features;

public static class Mappers
{
    public static Result<Rate> ToDomainModel(this RateDto rateDto)
    {
        var commentResult = Comment.Create(rateDto.Comment);

        if (commentResult.IsFailure)
        {
            return Result<Rate>.Failure(commentResult.Errors);
        }

        var authorId = rateDto.AuthorId;
        return Rate.Create(rateDto.Value, commentResult.Value, authorId);
    }

    public static Result<List<Rate>> ToDomainModels(this IEnumerable<RateDto> rateDtos)
    {
        var errors = new ErrorCollection();

        var rates = new List<Rate>();

        foreach (var rateDto in rateDtos)
        {
            var result = rateDto.ToDomainModel();

            errors.AddErrors(result.Errors);

            if (result.IsFailure)
            {
                continue;
            }

            rates.Add(result.Value);
        }

        if (errors.HasErrors())
        {
            return errors;
        }

        return Result<List<Rate>>.Success(rates);
    }

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
            Rates = training.Rates.Select(r => new RateDto
            {
                Value = r.Value,
                Comment = r.Comment.Content,
                AuthorId = r.AuthorId
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
            Rates = training.Rates.Select(r => new RateDto
            {
                Value = r.Value,
                Comment = r.Comment.Content,
                AuthorId = r.AuthorId
            }).ToList(),
            StartDate = training.StartDate,
            EndDate = training.EndDate,
            TrainerId = training.TrainerId
        }).ToList();
    }

    public static List<RateDto> ToDtos(this IEnumerable<Rate> rates)
    {
        return rates.Select(r => new RateDto
        {
            Value = r.Value, Comment = r.Comment.Content, AuthorId = r.AuthorId
        }).ToList();
    }
}
