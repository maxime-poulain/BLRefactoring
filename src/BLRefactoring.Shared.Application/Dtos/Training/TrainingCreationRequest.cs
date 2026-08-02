
namespace BLRefactoring.Shared.Application.Dtos.Training;

public sealed class TrainingCreationRequest
{
    public required string Title { get; init; }
    public required List<string> Topics { get; init; } = [];
    public required string Description { get; init; }
    public required string Prerequisites { get; init; }
    public required string AcquiredSkills { get; init; }
}

public sealed class TrainingEditionRequest
{
    public required string Title { get; init; }
    public required List<string> Topics { get; init; } = [];
    public required string Description { get; init; }
    public required string Prerequisites { get; init; }
    public required string AcquiredSkills { get; init; }
}
