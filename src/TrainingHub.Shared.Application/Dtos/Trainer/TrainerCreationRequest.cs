namespace TrainingHub.Shared.Application.Dtos.Trainer;

/// <summary>
/// What the application layer needs to create a trainer, before any of it has been
/// turned into value objects.
/// </summary>
public sealed class TrainerCreationRequest
{
    /// <summary>
    /// The trainer's first name, as the caller sent it.
    /// </summary>
    public required string Firstname { get; init; } = null!;

    /// <summary>
    /// The trainer's last name, as the caller sent it.
    /// </summary>
    public required string Lastname { get; init; } = null!;

    /// <summary>
    /// The initial contact address of the trainer. At registration it is seeded
    /// from the account email; the trainer can make it diverge afterwards through
    /// their profile.
    /// </summary>
    public required string ContactEmail { get; init; } = null!;

    /// <summary>
    /// The identity account the trainer is created for.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The trainer's biography, or <see langword="null"/> for none — absent at creation, cleared
    /// on edition.
    /// </summary>
    public string? Bio { get; init; }
}
