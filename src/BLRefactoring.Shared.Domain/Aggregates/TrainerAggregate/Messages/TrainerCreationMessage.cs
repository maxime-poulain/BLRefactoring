namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.Messages;

public class TrainerCreationMessage
{
    /// <summary>
    /// The identifier of the trainer to create, generated upfront by the caller
    /// so the primary key is known before the command completes.
    /// </summary>
    public required Guid TrainerId { get; init; }

    public required string Firstname { get; init; }
    public required string Lastname { get; init; }
    public required string Email { get; init; }
    public required string Bio { get; init; }
    public required Guid UserId { get; init; }
}