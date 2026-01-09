namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Messages;

public class TrainingEditionMessage
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Prerequisites { get; init; }
    public required string AcquiredSkills { get; init; }
    public required IEnumerable<string> Topics { get; init; }
}