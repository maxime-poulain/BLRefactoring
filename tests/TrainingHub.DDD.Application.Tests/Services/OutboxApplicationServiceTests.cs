using AwesomeAssertions;
using Moq;
using TrainingHub.DDD.Application.Services.OutboxServices;
using TrainingHub.Shared.Application.Dtos.Outbox;
using TrainingHub.Shared.Application.Outbox;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Common.Results;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.Services;

/// <summary>
/// Behavior covered for <c>OutboxApplicationService</c>.
/// </summary>
/// <remarks>
/// The layered half of the operator surface, and the twin of the CQRS handlers' facts on purpose:
/// both hosts publish the same two operations, so both have to reach the same port with the same
/// arguments and hand back what it answered (ADR 0061).
/// </remarks>
public sealed class OutboxApplicationServiceTests
{
    private readonly Mock<IOutboxOperations> _outboxOperations = new();

    /// <summary>
    /// Get poisoned async, the page asked for, asks the port for exactly that.
    /// </summary>
    [Fact]
    public async Task GetPoisonedAsync_ThePageAskedFor_AsksThePortForExactlyThat()
    {
        var paging = new PageRequest { Page = 3, PageSize = 5 };

        _outboxOperations
            .Setup(operations => operations.GetPoisonedAsync(It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PoisonedMessageDto>([], 3, 5, 0));

        var sut = new OutboxApplicationService(_outboxOperations.Object);

        await sut.GetPoisonedAsync(paging);

        _outboxOperations.Verify(
            operations => operations.GetPoisonedAsync(paging, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Get poisoned async, the page the port answered, hands it back untouched.
    /// </summary>
    [Fact]
    public async Task GetPoisonedAsync_ThePageThePortAnswered_HandsItBackUntouched()
    {
        var answered = new PagedResult<PoisonedMessageDto>(
            [
                new PoisonedMessageDto
                {
                    Id = Guid.CreateVersion7(),
                    Name = "TrainerCreated",
                    Version = 1,
                    OccurredOnUtc = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc),
                    Attempts = 5,
                    DeliveredConsumers = []
                }
            ],
            Page: 1,
            PageSize: 20,
            TotalCount: 1);

        _outboxOperations
            .Setup(operations => operations.GetPoisonedAsync(It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answered);

        var sut = new OutboxApplicationService(_outboxOperations.Object);

        var page = await sut.GetPoisonedAsync(new PageRequest());

        page.Should().BeSameAs(answered);
    }

    /// <summary>
    /// Requeue async, the message named, asks the port for exactly that one.
    /// </summary>
    [Fact]
    public async Task RequeueAsync_TheMessageNamed_AsksThePortForExactlyThatOne()
    {
        var messageId = Guid.CreateVersion7();

        _outboxOperations
            .Setup(operations => operations.RequeueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var sut = new OutboxApplicationService(_outboxOperations.Object);

        await sut.RequeueAsync(messageId);

        _outboxOperations.Verify(
            operations => operations.RequeueAsync(messageId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Requeue async, a refusal from the port, is carried out unchanged.
    /// </summary>
    /// <remarks>
    /// The codes are what the controller branches on to pick between 404 and 409, so a service that
    /// swallowed or relabelled one would turn a conflict into a bad request without anybody noticing.
    /// </remarks>
    [Fact]
    public async Task RequeueAsync_ARefusalFromThePort_IsCarriedOutUnchanged()
    {
        _outboxOperations
            .Setup(operations => operations.RequeueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OutboxErrorCodes.NotPoison, "still owed"));

        var sut = new OutboxApplicationService(_outboxOperations.Object);

        var result = await sut.RequeueAsync(Guid.CreateVersion7());

        result.Match(
            () => (IReadOnlyList<ErrorCode>)[],
            errors => [.. errors.Select(error => error.ErrorCode)])
            .Should().Equal([OutboxErrorCodes.NotPoison]);
    }
}
