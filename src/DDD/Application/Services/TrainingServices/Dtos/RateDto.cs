namespace BLRefactoring.DDD.Application.Services.TrainingServices.Dtos;

public class RateDto
{
    public int Value { get; init; }
    public string Comment { get; init; } = string.Empty;
    public Guid AuthorId { get; init; }
}
