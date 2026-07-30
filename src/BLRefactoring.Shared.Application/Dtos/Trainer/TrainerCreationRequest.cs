namespace BLRefactoring.Shared.Application.Dtos.Trainer;

public sealed class TrainerCreationRequest
{
    public required string Firstname { get; init; } = null!;
    public required string Lastname { get; init; } = null!;

    /// <summary>
    /// The initial contact address of the trainer. At registration it is seeded
    /// from the account email; the trainer can make it diverge afterwards through
    /// their profile.
    /// </summary>
    public required string ContactEmail { get; init; } = null!;

    public required Guid UserId { get; init; }
    public string? Bio { get; init; }
}
