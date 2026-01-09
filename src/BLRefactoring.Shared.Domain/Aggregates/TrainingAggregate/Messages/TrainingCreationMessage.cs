namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Messages;

public class TrainingCreationMessage : TrainingEditionMessage
{
    public required Guid TrainerId { get; init; }
    public required Guid UserId { get; init; }
}