using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace TrainingHub.Shared.Application.Dtos.Trainer;

/// <summary>
/// What narrows the administration's page of trainers, as the layered application layer takes it.
/// </summary>
/// <remarks>
/// The criteria travel as one <c>*Request</c> rather than as loose parameters, which is what
/// ADR 0048 asks of every layered signature: a name places the type without being read, and a
/// method taking a bare <c>TrainerStatus</c> said nothing about which boundary it sat on. A rule
/// caught that while this was being written, which is the whole point of having it.
/// <para>
/// The status arrives resolved. Turning a name into a <see cref="TrainerStatus"/> is the boundary's
/// job, because the boundary is where an unknown one can be answered with the parameter that
/// carried it; by the time this type exists there is nothing left to refuse.
/// </para>
/// </remarks>
public sealed class AdministrationTrainerRequest
{
    /// <summary>The standing to narrow to, or <see langword="null"/> for every trainer.</summary>
    public TrainerStatus? Status { get; init; }

    /// <summary>
    /// The term to look for in a name or a contact address, or <see langword="null"/> or blank for
    /// none.
    /// </summary>
    public string? Search { get; init; }
}
