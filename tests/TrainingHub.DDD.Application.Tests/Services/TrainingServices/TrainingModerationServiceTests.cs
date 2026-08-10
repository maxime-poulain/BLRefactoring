using AwesomeAssertions;
using TrainingHub.DDD.Application.Tests.Helpers;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.Services.TrainingServices;

/// <summary>
/// Behavior covered for the administrative use cases of <c>TrainingApplicationService</c>.
/// </summary>
/// <remarks>
/// The transitions and their refusals are the aggregate's rules and are proven in the domain suite.
/// What is proven here is what only this service decides: the reason is judged before the aggregate
/// is asked anything, so a motive the domain refuses leaves the training exactly as it was rather
/// than withheld with nothing on record — the mute state ADR 0052 forbids.
/// </remarks>
public sealed class TrainingModerationServiceTests
{
    private const string Motive = "Content reported as misleading.";

    private readonly TrainingServiceTestFixture _fixture = new();

    private async Task<Training> GivenTrainingAsync(Training? training = null)
    {
        var subject = training ?? await new TrainingBuilder().BuildValidAsync();

        _fixture.TrainingRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subject);

        return subject;
    }

    /// <summary>
    /// Withhold async, a published training, records the reason and commits once.
    /// </summary>
    [Fact]
    public async Task WithholdAsync_APublishedTraining_RecordsTheReasonAndCommitsOnce()
    {
        var training = await GivenTrainingAsync();

        var result = await _fixture.CreateSut().WithholdAsync(training.Id.Value, Motive);

        result.ShouldBeSuccess();
        training.Status.Should().Be(TrainingStatus.Withheld);
        training.WithholdingReason!.Value.Should().Be(Motive);
        _fixture.TrainingRepository.Verify(repository => repository.Update(training), Times.Once);
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Withhold async, a reason the domain refuses, leaves the training on offer.
    /// </summary>
    [Fact]
    public async Task WithholdAsync_AReasonTheDomainRefuses_LeavesTheTrainingOnOffer()
    {
        var training = await GivenTrainingAsync();

        var result = await _fixture.CreateSut().WithholdAsync(training.Id.Value, new string('x', 501));

        result.ShouldContainError(TrainingErrorCodes.WithholdingReasonTooLong);
        training.Status.Should().Be(TrainingStatus.Published);
        training.WithholdingReason.Should().BeNull();
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Withhold async, a training already withheld, keeps the reason it was holding.
    /// </summary>
    [Fact]
    public async Task WithholdAsync_ATrainingAlreadyWithheld_KeepsTheReasonItWasHolding()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.Withhold(WithholdingReason.Create("A first decision.").ShouldBeSuccess()).ShouldBeSuccess();
        await GivenTrainingAsync(training);

        var result = await _fixture.CreateSut().WithholdAsync(training.Id.Value, Motive);

        result.ShouldContainError(TrainingErrorCodes.AlreadyWithheld);
        training.WithholdingReason!.Value.Should().Be("A first decision.");
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Withhold async, a training that does not exist, answers not found.
    /// </summary>
    [Fact]
    public async Task WithholdAsync_ATrainingThatDoesNotExist_AnswersNotFound()
    {
        _fixture.TrainingRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Training?)null);

        var result = await _fixture.CreateSut().WithholdAsync(Guid.NewGuid(), Motive);

        result.ShouldContainError(ErrorCodes.NotFound);
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Release async, a withheld training, lands it on unpublished and commits once.
    /// </summary>
    [Fact]
    public async Task ReleaseAsync_AWithheldTraining_LandsItOnUnpublishedAndCommitsOnce()
    {
        var training = await new TrainingBuilder().BuildValidAsync();
        training.Withhold(WithholdingReason.Create(Motive).ShouldBeSuccess()).ShouldBeSuccess();
        await GivenTrainingAsync(training);

        var result = await _fixture.CreateSut().ReleaseAsync(training.Id.Value);

        result.ShouldBeSuccess();
        training.Status.Should().Be(TrainingStatus.Unpublished);
        training.WithholdingReason.Should().BeNull();
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Release async, a training that was not withheld, commits nothing.
    /// </summary>
    [Fact]
    public async Task ReleaseAsync_ATrainingThatWasNotWithheld_CommitsNothing()
    {
        var training = await GivenTrainingAsync();

        var result = await _fixture.CreateSut().ReleaseAsync(training.Id.Value);

        result.ShouldContainError(TrainingErrorCodes.NotWithheld);
        training.Status.Should().Be(TrainingStatus.Published);
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Release async, a training that does not exist, answers not found.
    /// </summary>
    [Fact]
    public async Task ReleaseAsync_ATrainingThatDoesNotExist_AnswersNotFound()
    {
        _fixture.TrainingRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<TrainingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Training?)null);

        var result = await _fixture.CreateSut().ReleaseAsync(Guid.NewGuid());

        result.ShouldContainError(ErrorCodes.NotFound);
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
