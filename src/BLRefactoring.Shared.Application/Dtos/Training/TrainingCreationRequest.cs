
namespace BLRefactoring.Shared.Application.Dtos.Training;

public class TrainingCreationRequest
{
    public required string Title { get; init; }
    public required List<string> Topics { get; init; } = [];
    public required string Description { get; init; }
    public required string Prerequisites { get; init; }
    public required string AcquiredSkills { get; init; }
}

public class TrainingEditionRequest
{
    public required Guid TrainingId { get; init; }
    public required string Title { get; init; }
    public required List<string> Topics { get; init; } = [];
    public required string Description { get; init; }
    public required string Prerequisites { get; init; }
    public required string AcquiredSkills { get; init; }
}
