namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.Messages;

public class TrainerCreationMessage : TrainerEditionMessage
{
    /// <summary>
    /// The identifier of the trainer to create, generated upfront by the caller
    /// so the primary key is known before the command completes.
    /// </summary>
    public required TrainerId TrainerId { get; init; }

    public required UserId UserId { get; init; }
}
