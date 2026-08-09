using TrainingHub.DDDWithCqrs.Application.Features.Outbox.GetPoisoned;
using TrainingHub.Shared.Application.Dtos.Outbox;
using TrainingHub.Shared.Application.Outbox;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Outbox.GetPoisoned;

/// <summary>
/// Answers the poison listing by asking the outbox's operator surface.
/// </summary>
/// <remarks>
/// A pass-through, like the catalogue's, and for the neighbouring reason: what usually separates
/// the two hosts is how each drives the write model, and there is no write model here at all. The
/// envelope is a persistence shape this layer must not name, so the port answers the DTO and the
/// handler carries it (ADR 0061).
/// </remarks>
public sealed class GetPoisonedMessagesQueryHandler(IOutboxOperations outboxOperations)
    : IQueryHandler<GetPoisonedMessagesQuery, PagedResult<PoisonedMessageDto>>
{
    /// <summary>
    /// Runs the query.
    /// </summary>
    public async ValueTask<PagedResult<PoisonedMessageDto>> Handle(
        GetPoisonedMessagesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await outboxOperations.GetPoisonedAsync(request.Paging, cancellationToken);
    }
}
