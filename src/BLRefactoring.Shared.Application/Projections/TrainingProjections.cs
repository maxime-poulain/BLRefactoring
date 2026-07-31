using System.Linq.Expressions;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.Shared.Application.Projections;

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
        AcquiredSkills = training.AcquiredSkills.Value
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
}
