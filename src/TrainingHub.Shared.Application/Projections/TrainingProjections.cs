using System.Linq.Expressions;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

namespace TrainingHub.Shared.Application.Projections;

/// <summary>
/// The single description of how a <see cref="Training"/> becomes a <see cref="TrainingDto"/>.
/// </summary>
/// <remarks>
/// Written once as an <see cref="Expression"/> and consumed two ways: the CQRS query handlers hand
/// <see cref="ToDtoExpression"/> to EF Core, which translates it into the <c>SELECT</c> list, while
/// the layered application services call <see cref="ToDto"/> on aggregates already in memory.
/// <para>
/// The expression is the source and the delegate the derivative, never the reverse — an expression
/// can always be compiled, a compiled delegate can never be translated to SQL. Keeping the two
/// mappings side by side, as they used to be, meant a field added to the DTO could reach one stack
/// and silently stay <see langword="null"/> on the other.
/// </para>
/// <para>
/// Consequence to keep in mind when editing: the body must stay EF-translatable, which is a
/// stricter rule than C#. Null-conditional access is the usual casualty — see
/// <see cref="TrainerProjections"/>, where a ternary stands in for <c>?.</c>.
/// </para>
/// </remarks>
public static class TrainingProjections
{
    /// <summary>
    /// The mapping itself, in the form EF Core can translate.
    /// </summary>
    public static readonly Expression<Func<Training, TrainingDto>> ToDtoExpression = training => new TrainingDto
    {
        RowVersion = training.RowVersion,
        Id = training.Id.Value,
        Title = training.Title.Value,
        TrainerId = training.TrainerId.Value,
        Topics = training.Topics.Select(topic => topic.Name).ToList(),
        Description = training.Description.Value,
        Prerequisites = training.Prerequisites.Value,
        AcquiredSkills = training.AcquiredSkills.Value,
        Status = training.Status.Name
    };

    // Compiled once for the lifetime of the process: compilation is expensive enough that doing
    // it per call would be a poor trade for what is otherwise a field-by-field copy.
    private static readonly Func<Training, TrainingDto> Compiled = ToDtoExpression.Compile();

    /// <summary>
    /// Maps an aggregate already loaded in memory.
    /// </summary>
    public static TrainingDto ToDto(this Training training) => Compiled(training);

    /// <summary>
    /// Maps a sequence of aggregates already loaded in memory.
    /// </summary>
    public static List<TrainingDto> ToDtos(this IEnumerable<Training> trainings) => [.. trainings.Select(ToDto)];

    /// <summary>
    /// The administration's mapping, in the form EF Core can translate.
    /// </summary>
    /// <remarks>
    /// A second expression rather than a superset of the first (ADR 0055): two audiences, two
    /// shapes, and no column that crosses between them by accident. This one names five columns
    /// where the other names nine — a moderation list does not read a training's content, and the
    /// difference is worth seeing in the <c>SELECT</c> rather than only in the DTO.
    /// </remarks>
    public static readonly Expression<Func<Training, AdministrationTrainingDto>> ToAdministrationDtoExpression =
        training => new AdministrationTrainingDto
        {
            Id = training.Id.Value,
            TrainerId = training.TrainerId.Value,
            Title = training.Title.Value,
            Status = training.Status.Name,
            WithholdingReason = training.WithholdingReason == null ? null : training.WithholdingReason.Value
        };

    private static readonly Func<Training, AdministrationTrainingDto> CompiledAdministration =
        ToAdministrationDtoExpression.Compile();

    /// <summary>
    /// Maps an aggregate already loaded in memory, for the administration.
    /// </summary>
    public static AdministrationTrainingDto ToAdministrationDto(this Training training) =>
        CompiledAdministration(training);
}
