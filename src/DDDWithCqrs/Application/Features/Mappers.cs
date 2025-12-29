using BLRefactoring.DDDWithCqrs.Application.Features.Trainings;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace BLRefactoring.DDDWithCqrs.Application.Features;

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
}
