namespace TrainingHub.Shared.Application.Dtos.Trainer;

/// <summary>
/// The new state of a trainer's profile. The whole profile is replaced, so a
/// <see langword="null"/> <see cref="Bio"/> clears the current one.
/// </summary>
public sealed class TrainerEditionRequest
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
    /// The address at which the trainer wishes to be contacted. Editing it has no
    /// effect on the identity account used to sign in.
    /// </summary>
    public required string ContactEmail { get; init; } = null!;

    /// <summary>
    /// The trainer's biography, or <see langword="null"/> for none — absent at creation, cleared
    /// on edition.
    /// </summary>
    public string? Bio { get; init; }
}
