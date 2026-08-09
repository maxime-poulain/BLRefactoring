using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace TrainingHub.Shared.Application.Dtos.Training;

/// <summary>
/// What narrows the administration's page of trainings, as the layered application layer takes it.
/// </summary>
/// <remarks>
/// The sibling of <c>AdministrationTrainerRequest</c>, and there for the same reason: ADR 0048 asks
/// that a layered signature say which boundary it is on, and it says so through the names of the
/// types it takes.
/// <para>
/// Two criteria, like its sibling. It carried one until ADR 0060, and the missing half was a
/// persistence fact rather than a field somebody forgot: a converted title is one EF Core cannot
/// look inside. Remapping it as a complex property is what closed the asymmetry ADR 0055 recorded.
/// </para>
/// </remarks>
public sealed class AdministrationTrainingRequest
{
    /// <summary>The state to narrow to, or <see langword="null"/> for every training.</summary>
    public TrainingStatus? Status { get; init; }

    /// <summary>
    /// The term to look for in a title, or <see langword="null"/> or blank for none.
    /// </summary>
    public string? Search { get; init; }
}
