using BLRefactoring.Shared.Common;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Edit;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Handlers;

public class EditTrainerCommandHandlerTests
{
    private readonly Mock<ITrainerRepository> _trainerRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private EditTrainerCommandHandler CreateSut() =>
        new(_trainerRepository.Object, _unitOfWork.Object);

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessUpdatesTrainerAndCommitsOnce()
    {
        var trainer = new TrainerBuilder().Build();
        GivenTrainer(trainer);
        var sut = CreateSut();

        var result = await sut.Handle(
            Command(trainer.Id.Value, firstname: "Jane", lastname: "Smith"),
            CancellationToken.None);

        result.ShouldBeSuccess();
        trainer.Name.Firstname.Should().Be("Jane");
        _trainerRepository.Verify(r => r.Update(trainer), Times.Once);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ChangedContactEmail_UpdatesTheContactAddress()
    {
        var trainer = new TrainerBuilder().Build();
        GivenTrainer(trainer);
        var sut = CreateSut();

        await sut.Handle(
            Command(trainer.Id.Value, contactEmail: "jane.smith@example.com"),
            CancellationToken.None);

        trainer.ContactEmail.FullAddress.Should().Be("jane.smith@example.com");
    }

    [Fact]
    public async Task Handle_NullBio_ClearsTheBio()
    {
        var trainer = new TrainerBuilder().WithBio("A bio to clear.").Build();
        GivenTrainer(trainer);
        var sut = CreateSut();

        var result = await sut.Handle(Command(trainer.Id.Value, bio: null), CancellationToken.None);

        result.ShouldBeSuccess();
        trainer.Bio.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UnknownTrainer_ReturnsNotFoundFailure()
    {
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);
        var sut = CreateSut();

        var result = await sut.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        result.ShouldContainError(ErrorCode.NotFound);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidDomainData_ReturnsFailureWithoutCommitting()
    {
        var trainer = new TrainerBuilder().Build();
        GivenTrainer(trainer);
        var sut = CreateSut();

        var result = await sut.Handle(
            Command(trainer.Id.Value, contactEmail: "invalid"),
            CancellationToken.None);

        result.ShouldBeFailure();
        _trainerRepository.Verify(r => r.Update(It.IsAny<Trainer>()), Times.Never);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_StaleVersion_ReturnsConcurrencyConflictWithoutCommitting()
    {
        var trainer = new TrainerBuilder().Build();
        GivenTrainer(trainer);
        var sut = CreateSut();

        var command = Command(trainer.Id.Value, firstname: "Jane");
        command.ExpectedVersion = [1, 2, 3, 4, 5, 6, 7, 8];

        var result = await sut.Handle(command, CancellationToken.None);

        result.ShouldContainError(ErrorCode.ConcurrencyConflict);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_StoreReportsAConflict_ReturnsConcurrencyConflict()
    {
        // The pre-check passed, but another request won the race before the update
        // reached the row: the concurrency token is the authoritative guard, and
        // both paths must surface the same business failure.
        var trainer = new TrainerBuilder().Build();
        GivenTrainer(trainer);
        _unitOfWork
            .Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("conflict", new Exception()));
        var sut = CreateSut();

        var result = await sut.Handle(
            Command(trainer.Id.Value, firstname: "Jane"), CancellationToken.None);

        result.ShouldContainError(ErrorCode.ConcurrencyConflict);
    }

    private void GivenTrainer(Trainer trainer) =>
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);

    private static EditTrainerCommand Command(
        Guid trainerId,
        string firstname = "John",
        string lastname = "Doe",
        string contactEmail = "john.doe@example.com",
        string? bio = "Experienced software trainer with 10 years of experience.")
        => new()
        {
            TrainerId = trainerId,
            Firstname = firstname,
            Lastname = lastname,
            ContactEmail = contactEmail,
            Bio = bio
        };
}
