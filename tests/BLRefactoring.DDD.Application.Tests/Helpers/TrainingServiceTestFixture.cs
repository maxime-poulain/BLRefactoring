using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using Moq;

namespace BLRefactoring.DDD.Application.Tests.Helpers;

public class TrainingServiceTestFixture
{
    public Mock<ITrainerRepository> TrainerRepository { get; } = new();
    public Mock<IUniquenessTitleChecker> TitleChecker { get; } = new();
    public Mock<ITrainingRepository> TrainingRepository { get; } = new();
    public Mock<ICurrentUserService> CurrentUserService { get; } = new();

    public TrainingApplicationService CreateSut() => new(
        TrainerRepository.Object,
        TitleChecker.Object,
        TrainingRepository.Object,
        CurrentUserService.Object);
}
