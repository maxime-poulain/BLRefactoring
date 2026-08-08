using AwesomeAssertions;
using TrainingHub.DDD.Application.Tests.Helpers;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.Services.TrainerServices;

/// <summary>
/// Behaviour covered for the administrative use cases of <c>TrainerApplicationService</c>.
/// </summary>
/// <remarks>
/// Whether a sanction is allowed, and what a reason may say, are the aggregate's and the value
/// object's rules and are proven where they live. What is proven here is what only this service
/// decides: it acts on the trainer the caller named rather than on the caller — the one pair of
/// writes on this service that does not read <c>ICurrentUserService</c> — and it judges the reason
/// before asking the aggregate for anything, so a motive the domain refuses leaves no trace.
/// </remarks>
public sealed class TrainerSanctionServiceTests
{
    private const string Motive = "Repeated breaches of the content policy.";

    private readonly TrainerServiceTestFixture _fixture = new();

    private Trainer GivenTrainer(Trainer? trainer = null)
    {
        var subject = trainer ?? new TrainerBuilder().Build();

        _fixture.TrainerRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subject);

        return subject;
    }

    /// <summary>
    /// Suspend async, an active trainer, records the reason and commits once.
    /// </summary>
    [Fact]
    public async Task SuspendAsync_AnActiveTrainer_RecordsTheReasonAndCommitsOnce()
    {
        var trainer = GivenTrainer();

        var result = await _fixture.CreateSut().SuspendAsync(trainer.Id.Value, Motive);

        result.ShouldBeSuccess();
        trainer.Status.Should().Be(TrainerStatus.Suspended);
        trainer.SuspensionReason!.Value.Should().Be(Motive);
        _fixture.TrainerRepository.Verify(repository => repository.Update(trainer), Times.Once);
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Suspend async, a trainer that is not the caller, sanctions the one the route named.
    /// </summary>
    /// <remarks>
    /// The distinguishing fact of the administrative surface, and the one a copy of the self-service
    /// shape would silently get wrong: every other write on this service resolves the trainer from
    /// the token, and this one must not. The caller here is somebody else entirely — an
    /// administrator's token carries no <c>trainer_id</c> at all (ADR 0051).
    /// </remarks>
    [Fact]
    public async Task SuspendAsync_ATrainerThatIsNotTheCaller_SanctionsTheOneTheRouteNamed()
    {
        var subject = GivenTrainer();
        _fixture.GivenCaller(Guid.NewGuid());

        var result = await _fixture.CreateSut().SuspendAsync(subject.Id.Value, Motive);

        result.ShouldBeSuccess();
        _fixture.TrainerRepository.Verify(
            repository => repository.GetByIdAsync(subject.Id, It.IsAny<CancellationToken>()), Times.Once);
        _fixture.CurrentUserService.VerifyGet(service => service.TrainerId, Times.Never);
    }

    /// <summary>
    /// Suspend async, a reason the domain refuses, leaves the trainer active.
    /// </summary>
    [Fact]
    public async Task SuspendAsync_AReasonTheDomainRefuses_LeavesTheTrainerActive()
    {
        var trainer = GivenTrainer();

        var result = await _fixture.CreateSut().SuspendAsync(trainer.Id.Value, string.Empty);

        result.ShouldContainError(TrainerErrorCodes.SuspensionReasonEmpty);
        trainer.Status.Should().Be(TrainerStatus.Active);
        trainer.SuspensionReason.Should().BeNull();
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Suspend async, a trainer that does not exist, answers not found.
    /// </summary>
    [Fact]
    public async Task SuspendAsync_ATrainerThatDoesNotExist_AnswersNotFound()
    {
        _fixture.TrainerRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);

        var result = await _fixture.CreateSut().SuspendAsync(Guid.NewGuid(), Motive);

        result.ShouldContainError(ErrorCodes.NotFound);
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Suspend async, a trainer already under sanction, keeps the reason it was holding.
    /// </summary>
    [Fact]
    public async Task SuspendAsync_ATrainerAlreadyUnderSanction_KeepsTheReasonItWasHolding()
    {
        var trainer = new TrainerBuilder().Build();
        trainer.Suspend(SuspensionReason.Create("A first sanction.").ShouldBeSuccess()).ShouldBeSuccess();
        GivenTrainer(trainer);

        var result = await _fixture.CreateSut().SuspendAsync(trainer.Id.Value, Motive);

        result.ShouldContainError(TrainerErrorCodes.AlreadySuspended);
        trainer.SuspensionReason!.Value.Should().Be("A first sanction.");
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Reinstate async, a suspended trainer, clears the reason and commits once.
    /// </summary>
    [Fact]
    public async Task ReinstateAsync_ASuspendedTrainer_ClearsTheReasonAndCommitsOnce()
    {
        var trainer = new TrainerBuilder().Build();
        trainer.Suspend(SuspensionReason.Create(Motive).ShouldBeSuccess()).ShouldBeSuccess();
        GivenTrainer(trainer);

        var result = await _fixture.CreateSut().ReinstateAsync(trainer.Id.Value);

        result.ShouldBeSuccess();
        trainer.Status.Should().Be(TrainerStatus.Active);
        trainer.SuspensionReason.Should().BeNull();
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Reinstate async, a trainer that is not under sanction, commits nothing.
    /// </summary>
    [Fact]
    public async Task ReinstateAsync_ATrainerThatIsNotUnderSanction_CommitsNothing()
    {
        var trainer = GivenTrainer();

        var result = await _fixture.CreateSut().ReinstateAsync(trainer.Id.Value);

        result.ShouldContainError(TrainerErrorCodes.NotSuspended);
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Reinstate async, a trainer that does not exist, answers not found.
    /// </summary>
    [Fact]
    public async Task ReinstateAsync_ATrainerThatDoesNotExist_AnswersNotFound()
    {
        _fixture.TrainerRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);

        var result = await _fixture.CreateSut().ReinstateAsync(Guid.NewGuid());

        result.ShouldContainError(ErrorCodes.NotFound);
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
