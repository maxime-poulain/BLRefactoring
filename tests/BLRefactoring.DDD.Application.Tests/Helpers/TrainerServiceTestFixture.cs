using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using Moq;

namespace BLRefactoring.DDD.Application.Tests.Helpers;

public class TrainerServiceTestFixture
{
    public Mock<ITrainerRepository> TrainerRepository { get; } = new();
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    public TrainerApplicationService CreateSut() => new(
        TrainerRepository.Object,
        UnitOfWork.Object);
}
