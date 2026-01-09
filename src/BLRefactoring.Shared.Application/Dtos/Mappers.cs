using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.Application.Dtos.Training;

namespace BLRefactoring.Shared.Application.Dtos;

public static class Mappers
{
    public static TrainingDto ToDto(this Domain.Aggregates.TrainingAggregate.Training training)
    {
        return new TrainingDto
        {
            Id = training.Id,
            Title = training.Title.Value,
            TrainerId = training.TrainerId,
            Topics = training.Topics.Select(t => t.Name).ToList(),
            Description = training.Description.Value,
            Prerequisites = training.Prerequisites.Value,
            AcquiredSkills = training.AcquiredSkills.Value
        };
    }

    public static List<TrainingDto> ToDtos(this IEnumerable<Domain.Aggregates.TrainingAggregate.Training> trainings)
    {
        return trainings.Select(ToDto).ToList();
    }

    public static TrainerDto ToDto(this Domain.Aggregates.TrainerAggregate.Trainer trainer)
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
