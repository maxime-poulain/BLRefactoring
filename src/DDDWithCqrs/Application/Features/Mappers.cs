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

        return commentResult.Match(
            comment => Rate.Create(rateDto.Value, comment, rateDto.AuthorId),
            Result<Rate>.Failure);
    }

    public static Result<List<Rate>> ToDomainModels(this IEnumerable<RateDto> rateDtos)
    {
        var errors = new ErrorCollection();
        var rates = new List<Rate>();

        foreach (var rateDto in rateDtos)
        {
            rateDto.ToDomainModel().Match(
                rate =>
                {
                    rates.Add(rate);
                    return Result.Success();
                },
                errorValue =>
                {
                    errors.AddErrors(errorValue);
                    return Result.Failure(errors);
                });
        }

        return errors.Match(() => Result<List<Rate>>.Success(rates), Result<List<Rate>>.Failure);
    }

    public static List<Rate> ToRates(this List<RateDto> rates)
    {
        return rates.Select(rateDto =>
            Rate.Create(
                rateDto.Value,
                Comment.Create(rateDto.Comment)
                    .Match(
                        comment => comment,
                        err => throw new AbandonedMutexException()),
                rateDto.AuthorId).Match(rate => rate, errors => null)).ToList();
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
