using TrainingHub.Shared.Application.Dtos.Outbox;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Outbox.GetPoisoned;

/// <summary>
/// One page of the messages delivery gave up on (ADR 0061).
/// </summary>
/// <remarks>
/// The second query here that reads no aggregate table, and it does not read a read model either:
/// it reads the platform's own delivery table, which is neither. What it has in common with
/// <c>SearchCatalogQuery</c> is the shape of the answer — a page of a DTO — and nothing else.
/// <para>
/// It carries no filter. A poison message is defined by the configured retry budget rather than by
/// anything a caller could ask for, so there is no criterion to offer and offering one would invite
/// a caller to describe a set the mechanism does not have.
/// </para>
/// </remarks>
public sealed class GetPoisonedMessagesQuery : IQuery<PagedResult<PoisonedMessageDto>>
{
    /// <summary>
    /// The page asked for; the default page when none was.
    /// </summary>
    public PageRequest Paging { get; init; } = new();
}
