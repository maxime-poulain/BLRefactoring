namespace BLRefactoring.DDD.Application.Services.TrainingServices.Dtos;

public class TrainingCreationRequest
{
    public string Title { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public Guid TrainerId { get; init; }
    public List<RateDto> Rates { get; init; } = new();
}
