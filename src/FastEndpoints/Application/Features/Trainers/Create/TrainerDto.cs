namespace BLRefactoring.FastEndpoints.Application.Features.Trainers.Create;

public class TrainerDto
{
    public Guid Id { get; init; }
    public string Firstname { get; init; } = null!;
    public string Lastname { get; init; } = null!;
    public string Email { get; init; } = null!;
}
