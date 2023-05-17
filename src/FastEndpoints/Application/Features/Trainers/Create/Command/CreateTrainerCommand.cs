using FastEndpoints;

namespace BLRefactoring.FastEndpoints.Application.Features.Trainers.Create.Command;

/// <summary>
/// Represents a command to create a trainer.
/// </summary>
public class CreateTrainerCommand : ICommand<TrainerDto>
{
    public string Firstname { get; init; } = null!;
    public string Lastname { get; init; } = null!;
    public string Email { get; init; } = null!;
}
