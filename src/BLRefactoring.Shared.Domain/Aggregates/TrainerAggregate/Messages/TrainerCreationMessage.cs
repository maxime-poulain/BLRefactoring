namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.Messages;

public class TrainerCreationMessage
{
    public required string Firstname { get; init; }
    public required string Lastname { get; init; }
    public required string Email { get; init; }
    public required string Bio { get; init; }
    public required Guid UserId { get; init; }
}