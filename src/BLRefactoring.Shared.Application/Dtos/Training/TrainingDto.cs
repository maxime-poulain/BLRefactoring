namespace BLRefactoring.Shared.Application.Dtos.Training;

public class TrainingDto
{
    public required Guid Id { get; set; }
    public required string Title { get; set; } = string.Empty;
    public required Guid TrainerId { get; set; }
    public required List<string> Topics { get; set; } = [];
    public required string Description { get; set; }
    public required string Prerequisites { get; set; }
    public required string AcquiredSkills { get; set; }
}
