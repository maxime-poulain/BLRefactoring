using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace TrainingHub.Shared.Api.Contracts.Administration;

/// <summary>
/// What narrows <c>GET /Administration/trainings</c>, bound from the query string.
/// </summary>
/// <remarks>
/// One criterion, where the trainers' filter carries two, and the asymmetry is worth stating rather
/// than leaving a reader to notice. A training's title is mapped through a value converter, and EF
/// Core can compare a converted property for equality but cannot look inside it — neither
/// <c>Title.Value.Contains</c> nor <c>EF.Property&lt;string&gt;</c> translates. Making the title
/// searchable means remapping it as a complex property, and that column carries the unique index
/// that enforces title uniqueness under concurrency: a migration, and a decision of its own
/// (ADR 0055).
/// <para>
/// What is here is what a moderator actually asks for. Nothing else in this API can find a withheld
/// training, and <c>?status=Withheld</c> is that question.
/// </para>
/// </remarks>
public sealed class AdministrationTrainingFilterHttpRequest
{
    /// <summary>
    /// Only trainings in this state, or all of them when it is absent.
    /// </summary>
    [KnownStatus(typeof(TrainingStatus))]
    public string? Status { get; init; }
}
