using AwesomeAssertions;
using Moq;
using TrainingHub.DDDWithCqrs.Application.Features.Outbox.GetPoisoned;
using TrainingHub.DDDWithCqrs.Application.Features.Outbox.Requeue;
using TrainingHub.DDDWithCqrs.Infrastructure.Features.Outbox.GetPoisoned;
using TrainingHub.Shared.Application.Dtos.Outbox;
using TrainingHub.Shared.Application.Outbox;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Common.Results;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behavior covered for the two handlers of the outbox's operator surface (ADR 0061).
/// </summary>
/// <remarks>
/// One file for a query and a command, because the pair is the surface: what is worth pinning is
/// that each reaches the port with what its message carried and adds nothing of its own. What the
/// port then does is exercised against a real database in <c>OutboxOperationsTests</c>.
/// </remarks>
public sealed class OutboxOperationHandlerTests
{
    private readonly Mock<IOutboxOperations> _outboxOperations = new();

    /// <summary>
    /// Handle, the page the query carried, asks the port for exactly that.
    /// </summary>
    [Fact]
    public async Task Handle_ThePageTheQueryCarried_AsksThePortForExactlyThat()
    {
        var paging = new PageRequest { Page = 4, PageSize = 5 };

        _outboxOperations
            .Setup(operations => operations.GetPoisonedAsync(paging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PoisonedMessageDto>([], 4, 5, 0));

        var sut = new GetPoisonedMessagesQueryHandler(_outboxOperations.Object);

        await sut.Handle(new GetPoisonedMessagesQuery { Paging = paging }, CancellationToken.None);

        _outboxOperations.Verify(
            operations => operations.GetPoisonedAsync(paging, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, the page the port answered, hands it back untouched.
    /// </summary>
    /// <remarks>
    /// The counts and the position have to cross this handler unchanged: rebuilding the envelope is
    /// how a count and its items end up describing different sets (ADR 0029).
    /// </remarks>
    [Fact]
    public async Task Handle_ThePageThePortAnswered_HandsItBackUntouched()
    {
        var answered = new PagedResult<PoisonedMessageDto>(
            [
                new PoisonedMessageDto
                {
                    Id = Guid.CreateVersion7(),
                    Name = "TrainingWithheld",
                    Version = 1,
                    OccurredOnUtc = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc),
                    Attempts = 5,
                    DeliveredConsumers = ["SearchIndex"]
                }
            ],
            Page: 2,
            PageSize: 1,
            TotalCount: 7);

        _outboxOperations
            .Setup(operations => operations.GetPoisonedAsync(
                It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answered);

        var sut = new GetPoisonedMessagesQueryHandler(_outboxOperations.Object);

        var page = await sut.Handle(new GetPoisonedMessagesQuery(), CancellationToken.None);

        page.Should().BeSameAs(answered);
    }

    /// <summary>
    /// Handle, the message the command named, asks the port for exactly that one.
    /// </summary>
    [Fact]
    public async Task Handle_TheMessageTheCommandNamed_AsksThePortForExactlyThatOne()
    {
        var messageId = Guid.CreateVersion7();

        _outboxOperations
            .Setup(operations => operations.RequeueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var sut = new RequeueOutboxMessageCommandHandler(_outboxOperations.Object);

        await sut.Handle(new RequeueOutboxMessageCommand(messageId), CancellationToken.None);

        _outboxOperations.Verify(
            operations => operations.RequeueAsync(messageId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, a refusal from the port, is carried out unchanged.
    /// </summary>
    /// <remarks>
    /// The codes are what the controller branches on to pick between 404 and 409, so a handler that
    /// relabelled one would turn a conflict into a bad request without anybody noticing.
    /// </remarks>
    [Fact]
    public async Task Handle_ARefusalFromThePort_IsCarriedOutUnchanged()
    {
        _outboxOperations
            .Setup(operations => operations.RequeueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ErrorCodes.NotFound, "nobody stored it"));

        var sut = new RequeueOutboxMessageCommandHandler(_outboxOperations.Object);

        var result = await sut.Handle(new RequeueOutboxMessageCommand(Guid.CreateVersion7()), CancellationToken.None);

        result.Match(
            () => (IReadOnlyList<ErrorCode>)[],
            errors => [.. errors.Select(error => error.ErrorCode)])
            .Should().Equal([ErrorCodes.NotFound]);
    }
}
