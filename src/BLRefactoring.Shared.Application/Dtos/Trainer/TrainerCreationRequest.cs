namespace BLRefactoring.Shared.Application.Dtos.Trainer;

public sealed class TrainerCreationRequest
{
    public required string Firstname { get; init; } = null!;
    public required string Lastname { get; init; } = null!;
    public required string Email { get; init; } = null!;
    public required Guid UserId { get; init; }
    public required string Bio { get; init; } = null!;
}
