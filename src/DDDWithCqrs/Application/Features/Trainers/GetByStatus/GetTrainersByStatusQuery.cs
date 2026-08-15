using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetByStatus;

/// <summary>
/// One page of trainers as the administration reads them, newest first (ADR 0055).
/// </summary>
/// <remarks>
/// The mirror image of <c>GetTrainingsByCurrentTrainerQuery</c>, and the contrast is the point: that one
/// carries no identity because there is none for a caller to get wrong, and this one carries no
/// identity because it is not about one. It reads across every trainer, which is what this API
/// withdrew when it deleted <c>/Trainer/all</c> and what ADR 0055 gives back to a single role.
/// <para>
/// The criteria are named one by one and travel as strings. A <c>TrainerStatus</c> would be a nicer
/// field and the wrong one: a query is a message, and a message that reaches a dispatcher from
/// somewhere other than a controller carries whatever its sender put in it. Strings are what the
/// validator can refuse and what the handler resolves once, which is the same shape every
/// identifier on this stack already has (ADR 0046).
/// </para>
/// </remarks>
public sealed class GetTrainersByStatusQuery : IQuery<PagedResult<AdministrationTrainerDto>>
{
    /// <summary>
    /// The standing to narrow to, or <see langword="null"/> for every trainer.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// The term to look for in a name or a contact address, or <see langword="null"/> for none.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// The page asked for; the default page when none was.
    /// </summary>
    public PageRequest Paging { get; init; } = new();
}
