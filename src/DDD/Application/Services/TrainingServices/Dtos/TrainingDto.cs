namespace BLRefactoring.DDD.Application.Services.TrainingServices.Dtos;

public class TrainingDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public Guid TrainerId { get; set; }
    public List<RateDto> Rates { get; set; } = new();
}
