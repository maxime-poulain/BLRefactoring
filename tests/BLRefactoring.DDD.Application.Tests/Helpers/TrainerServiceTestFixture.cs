using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using Microsoft.Extensions.Logging;
using Moq;

namespace BLRefactoring.DDD.Application.Tests.Helpers;

public class TrainerServiceTestFixture
{
    public Mock<ITrainerRepository> TrainerRepository { get; } = new();
    public Mock<ITransactionManager> TransactionManager { get; } = new();
    public Mock<ILogger<TrainerApplicationService>> Logger { get; } = new();

    public TrainerApplicationService CreateSut() => new(
        Logger.Object,
        TrainerRepository.Object,
        TransactionManager.Object);
}
