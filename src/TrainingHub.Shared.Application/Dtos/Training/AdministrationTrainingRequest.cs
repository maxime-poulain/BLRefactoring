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
/// One criterion where its sibling has two. A title is value-converted, and EF Core cannot look
/// inside a converted property, so a substring match on it does not translate — a persistence fact
/// recorded in ADR 0055 rather than a field somebody forgot.
/// </para>
/// </remarks>
public sealed class AdministrationTrainingRequest
{
    /// <summary>The state to narrow to, or <see langword="null"/> for every training.</summary>
    public TrainingStatus? Status { get; init; }
}
