using System.ComponentModel.DataAnnotations;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace TrainingHub.Shared.Api.Contracts.Administration;

/// <summary>
/// What narrows <c>GET /Administration/trainers</c>, bound from the query string.
/// </summary>
/// <remarks>
/// Two criteria and no more, which is the line ADR 0055 draws: a status the domain declares, and a
/// term to look for. Neither is a predicate, and there is no sort — the order is the one every paged
/// read here uses, so the two hosts cannot page differently (ADR 0001, ADR 0029).
/// <para>
/// Bound as a second <c>[FromQuery]</c> object beside <c>PaginationHttpRequest</c> rather than
/// carrying page coordinates of its own, so the bounds ADR 0029 published exist in one place and
/// <c>PageRequest.MaxPageSize</c> stays the only source of the cap.
/// </para>
/// </remarks>
public sealed class AdministrationTrainerFilterHttpRequest
{
    /// <summary>
    /// Only trainers in this state, or all of them when it is absent.
    /// </summary>
    [KnownStatus(typeof(TrainerStatus))]
    public string? Status { get; init; }

    /// <summary>
    /// Only trainers whose name or contact address contains this, or all of them when it is absent.
    /// </summary>
    /// <remarks>
    /// Bounded so that a caller cannot send a term longer than any of the columns it is compared
    /// against. What the term costs to answer is recorded in ADR 0055 rather than hidden: it is a
    /// <c>LIKE '%term%'</c> against the write model, which no index can seek, and the page is what
    /// bounds the damage rather than avoids it.
    /// </remarks>
    [StringLength(100)]
    public string? Search { get; init; }
}
