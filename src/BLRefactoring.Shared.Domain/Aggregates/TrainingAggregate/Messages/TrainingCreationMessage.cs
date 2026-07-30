namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Messages;

public class TrainingCreationMessage : TrainingEditionMessage
{
    /// <summary>
    /// The identifier of the training to create, generated upfront by the caller
    /// so the primary key is known before the command completes.
    /// </summary>
    public required Guid TrainingId { get; init; }

    public required Guid TrainerId { get; init; }
    public required Guid UserId { get; init; }
}