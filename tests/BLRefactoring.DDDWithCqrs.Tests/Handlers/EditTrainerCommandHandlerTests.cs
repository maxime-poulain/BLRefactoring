using AwesomeAssertions;
using BLRefactoring.Shared.Common;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Edit;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behaviour covered for <c>EditTrainerCommandHandler</c>.
/// </summary>
public sealed class EditTrainerCommandHandlerTests
{
    private readonly Mock<ITrainerRepository> _trainerRepository = new();
    private readonly Mock<ICurrentUserService> _currentUserService = new();
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private EditTrainerCommandHandler CreateSut() =>
        new(_trainerRepository.Object, _currentUserService.Object, _unitOfWork.Object);

    /// <summary>
    /// Edit trainer command handler tests.
    /// </summary>
    public EditTrainerCommandHandlerTests()
    {
        // The trainer being edited is no longer a field on the command: the handler resolves it.
        // Every test therefore needs a caller, and a non-empty one -- TrainerId.Create refuses
        // Guid.Empty, so a default mock would throw before any assertion was reached. GivenTrainer
        // narrows it to the trainer under test where there is one.
        _currentUserService.SetupGet(service => service.TrainerId).Returns(_callerId);
    }

    /// <summary>
    /// Handle, valid command, returns success updates trainer and commits once.
    /// </summary>
    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessUpdatesTrainerAndCommitsOnce()
    {
        var trainer = new TrainerBuilder().Build();
        GivenTrainer(trainer);
        var sut = CreateSut();

        var result = await sut.Handle(
            Command(firstname: "Jane", lastname: "Smith"),
            CancellationToken.None);

        result.ShouldBeSuccess();
        trainer.Name.Firstname.Should().Be("Jane");
        _trainerRepository.Verify(r => r.Update(trainer), Times.Once);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, changed contact email, updates the contact address.
    /// </summary>
    [Fact]
    public async Task Handle_ChangedContactEmail_UpdatesTheContactAddress()
    {
        var trainer = new TrainerBuilder().Build();
        GivenTrainer(trainer);
        var sut = CreateSut();

        await sut.Handle(
            Command(contactEmail: "jane.smith@example.com"),
            CancellationToken.None);

        trainer.ContactEmail.FullAddress.Should().Be("jane.smith@example.com");
    }

    /// <summary>
    /// Handle, null bio, clears the bio.
    /// </summary>
    [Fact]
    public async Task Handle_NullBio_ClearsTheBio()
    {
        var trainer = new TrainerBuilder().WithBio("A bio to clear.").Build();
        GivenTrainer(trainer);
        var sut = CreateSut();

        var result = await sut.Handle(Command(bio: null), CancellationToken.None);

        result.ShouldBeSuccess();
        trainer.Bio.Should().BeNull();
    }

    /// <summary>
    /// Handle, unknown trainer, returns not found failure.
    /// </summary>
    [Fact]
    public async Task Handle_UnknownTrainer_ReturnsNotFoundFailure()
    {
        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);
        var sut = CreateSut();

        var result = await sut.Handle(Command(), CancellationToken.None);

        result.ShouldContainError(ErrorCodes.NotFound);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Handle, invalid domain data, returns failure without committing.
    /// </summary>
    [Fact]
    public async Task Handle_InvalidDomainData_ReturnsFailureWithoutCommitting()
    {
        var trainer = new TrainerBuilder().Build();
        GivenTrainer(trainer);
        var sut = CreateSut();

        var result = await sut.Handle(
            Command(contactEmail: "invalid"),
            CancellationToken.None);

        result.ShouldBeFailure();
        _trainerRepository.Verify(r => r.Update(It.IsAny<Trainer>()), Times.Never);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Handle, stale version, returns concurrency conflict without committing.
    /// </summary>
    [Fact]
    public async Task Handle_StaleVersion_ReturnsConcurrencyConflictWithoutCommitting()
    {
        var trainer = new TrainerBuilder().Build();
        GivenTrainer(trainer);
        var sut = CreateSut();

        var command = Command(expectedVersion: [1, 2, 3, 4, 5, 6, 7, 8], firstname: "Jane");

        var result = await sut.Handle(command, CancellationToken.None);

        result.ShouldContainError(ErrorCodes.ConcurrencyConflict);
        _unitOfWork.Verify(
            uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Handle, store reports a conflict, returns concurrency conflict.
    /// </summary>
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
            .ThrowsAsync(new ConcurrencyConflictException("conflict", new InvalidOperationException("inner")));
        var sut = CreateSut();

        var result = await sut.Handle(
            Command(firstname: "Jane"), CancellationToken.None);

        result.ShouldContainError(ErrorCodes.ConcurrencyConflict);
    }

    private void GivenTrainer(Trainer trainer)
    {
        _currentUserService.SetupGet(service => service.TrainerId).Returns(trainer.Id.Value);

        _trainerRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);
    }

    private static EditTrainerCommand Command(
        byte[]? expectedVersion = null,
        string firstname = "John",
        string lastname = "Doe",
        string contactEmail = "john.doe@example.com",
        string? bio = "Experienced software trainer with 10 years of experience.")
        => new()
        {
            ExpectedVersion = expectedVersion ?? [],
            Firstname = firstname,
            Lastname = lastname,
            ContactEmail = contactEmail,
            Bio = bio
        };
}
