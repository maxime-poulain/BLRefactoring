namespace BLRefactoring.Shared.Application.Dtos.Trainer;

public class TrainerDto
{
    public required Guid Id { get; init; }
    public required string Firstname { get; init; } = null!;
    public required string Lastname { get; init; } = null!;
    public required string Email { get; init; } = null!;
}
