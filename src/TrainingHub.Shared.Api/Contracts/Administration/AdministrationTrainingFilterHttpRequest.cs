using System.ComponentModel.DataAnnotations;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace TrainingHub.Shared.Api.Contracts.Administration;

/// <summary>
/// What narrows <c>GET /Administration/trainings</c>, bound from the query string.
/// </summary>
/// <remarks>
/// Two criteria and no more, which is the line ADR 0055 draws: a status the domain declares, and a
/// term to look for. It carried one until ADR 0060, and the missing half was a persistence fact
/// rather than a choice — a converted column is one EF Core compares for equality without being
/// able to look inside. The title is a complex property now, so it can.
/// <para>
/// Bound as a second <c>[FromQuery]</c> object beside <c>PaginationHttpRequest</c> rather than
/// carrying page coordinates of its own, so the bounds ADR 0029 published exist in one place and
/// <c>PageRequest.MaxPageSize</c> stays the only source of the cap.
/// </para>
/// </remarks>
public sealed class AdministrationTrainingFilterHttpRequest
{
    /// <summary>
    /// Only trainings in this state, or all of them when it is absent.
    /// </summary>
    /// <remarks>
    /// What a moderator most often asks: nothing else in this API can find a withheld training, and
    /// <c>?status=Withheld</c> is that question.
    /// </remarks>
    [KnownStatus(typeof(TrainingStatus))]
    public string? Status { get; init; }

    /// <summary>
    /// Only trainings whose title contains this, or all of them when it is absent.
    /// </summary>
    /// <remarks>
    /// Bounded to the length of the column it is compared against — a term longer than any title
    /// can match nothing, so refusing it says more than an empty page would.
    /// <para>
    /// The title and nothing else. A description and a set of prerequisites are prose, and searching
    /// them turns a bounded scan into an unbounded one for matches nobody asked for — the argument
    /// ADR 0055 already makes for keeping a trainer's bio out of their term.
    /// </para>
    /// <para>
    /// It costs the same <c>LIKE '%term%'</c> the trainers' listing pays, and for the same recorded
    /// reason: no index can seek one. The search that seeks is the public catalogue's, over the
    /// inverted index (ADR 0059) — which cannot answer this one, because it holds none of the states
    /// a moderator is looking for.
    /// </para>
    /// </remarks>
    [StringLength(100)]
    public string? Search { get; init; }
}
