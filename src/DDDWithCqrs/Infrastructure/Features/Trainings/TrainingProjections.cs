using System.Linq.Expressions;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.Features.Trainings;

/// <summary>
/// Projections from the <see cref="Training"/> aggregate to its read DTOs,
/// shared by the training query handlers.
/// </summary>
public static class TrainingProjections
{
    public static readonly Expression<Func<Training, TrainingDto>> ToDto = training => new TrainingDto
    {
        RowVersion = training.RowVersion,
        Id = training.Id.Value,
        Title = training.Title.Value,
        TrainerId = training.TrainerId.Value,
        Topics = training.Topics.Select(topic => topic.Name).ToList(),
        Description = training.Description.Value,
        Prerequisites = training.Prerequisites.Value,
        AcquiredSkills = training.AcquiredSkills.Value
    };
}
