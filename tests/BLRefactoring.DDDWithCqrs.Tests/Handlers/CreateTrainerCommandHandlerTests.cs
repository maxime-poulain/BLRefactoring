using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behaviour covered for <c>CreateTrainerCommandHandler</c>.
/// </summary>
public sealed class CreateTrainerCommandHandlerTests
{
    private readonly Mock<ITrainerRepository> _trainerRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private CreateTrainerCommandHandler CreateSut() =>
        new(_trainerRepository.Object, _unitOfWork.Object);

    /// <summary>
    /// Handle, valid command, returns success adds trainer and commits once.
    /// </summary>
    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessAddsTrainerAndCommitsOnce()
    {
        var command = new CreateTrainerCommand
        {
            Firstname = "John",
            Lastname = "Doe",
            ContactEmail = "john.doe@example.com",
            UserId = Guid.NewGuid()
        };
        var sut = CreateSut();

        var result = await sut.Handle(command, CancellationToken.None);

        result.ShouldBeSuccess();
        _trainerRepository.Verify(r => r.Add(It.IsAny<Trainer>()), Times.Once);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, invalid domain data, returns failure.
    /// </summary>
    [Fact]
    public async Task Handle_InvalidDomainData_ReturnsFailure()
    {
        var command = new CreateTrainerCommand
        {
            Firstname = "J", // too short — domain rejects
            Lastname = "Doe",
            ContactEmail = "john.doe@example.com",
            UserId = Guid.NewGuid()
        };
        var sut = CreateSut();

        var result = await sut.Handle(command, CancellationToken.None);

        result.ShouldBeFailure();
    }

    /// <summary>
    /// Handle, invalid domain data, does not add nor commit.
    /// </summary>
    [Fact]
    public async Task Handle_InvalidDomainData_DoesNotAddNorCommit()
    {
        var command = new CreateTrainerCommand
        {
            Firstname = "J",
            Lastname = "Doe",
            ContactEmail = "invalid",
            UserId = Guid.NewGuid()
        };
        var sut = CreateSut();

        await sut.Handle(command, CancellationToken.None);

        _trainerRepository.Verify(r => r.Add(It.IsAny<Trainer>()), Times.Never);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
