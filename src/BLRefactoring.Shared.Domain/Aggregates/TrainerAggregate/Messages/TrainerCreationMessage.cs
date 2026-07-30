namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.Messages;

public class TrainerCreationMessage
{
    /// <summary>
    /// The identifier of the trainer to create, generated upfront by the caller
    /// so the primary key is known before the command completes.
    /// </summary>
    public required TrainerId TrainerId { get; init; }

    public required string Firstname { get; init; }
    public required string Lastname { get; init; }
    public required string Email { get; init; }

    /// <summary>
    /// The bio of the trainer. <see langword="null"/> means no bio was provided,
    /// which is allowed at creation time; a provided value (even empty) is
    /// validated by the <c>Bio</c> value object.
    /// </summary>
    public string? Bio { get; init; }
    public required UserId UserId { get; init; }
}