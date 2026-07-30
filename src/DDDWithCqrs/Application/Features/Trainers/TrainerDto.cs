namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers;

public class TrainerDto
{
    public Guid Id { get; init; }
    public string Firstname { get; init; } = null!;
    public string Lastname { get; init; } = null!;

    /// <summary>
    /// The address at which the trainer wishes to be contacted, which is not the
    /// email of their identity account.
    /// </summary>
    public string ContactEmail { get; init; } = null!;

    /// <summary>
    /// The bio of the trainer, or <see langword="null"/> when none was provided.
    /// </summary>
    public string? Bio { get; init; }
}
