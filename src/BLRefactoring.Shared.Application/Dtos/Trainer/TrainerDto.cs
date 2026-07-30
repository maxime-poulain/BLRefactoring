namespace BLRefactoring.Shared.Application.Dtos.Trainer;

public class TrainerDto
{
    public required Guid Id { get; init; }
    public required string Firstname { get; init; } = null!;
    public required string Lastname { get; init; } = null!;

    /// <summary>
    /// The address at which the trainer wishes to be contacted, which is not the
    /// email of their identity account.
    /// </summary>
    public required string ContactEmail { get; init; } = null!;

    /// <summary>
    /// The bio of the trainer, or <see langword="null"/> when none was provided.
    /// </summary>
    public string? Bio { get; init; }
}
