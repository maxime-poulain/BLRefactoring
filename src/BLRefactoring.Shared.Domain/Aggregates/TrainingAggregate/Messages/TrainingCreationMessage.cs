using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Messages;

public class TrainingCreationMessage : TrainingEditionMessage
{
    /// <summary>
    /// The identifier of the training to create, generated upfront by the caller
    /// so the primary key is known before the command completes.
    /// </summary>
    public required TrainingId TrainingId { get; init; }

    public required TrainerId TrainerId { get; init; }
}