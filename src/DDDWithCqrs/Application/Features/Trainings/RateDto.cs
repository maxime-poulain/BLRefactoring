namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings;

public class RateDto
{
    public int Value { get; init; }
    public string Comment { get; init; } = string.Empty;

    // For simplicity we assume there is an aggregate Person/Author.
    public Guid AuthorId { get; init; }
}
