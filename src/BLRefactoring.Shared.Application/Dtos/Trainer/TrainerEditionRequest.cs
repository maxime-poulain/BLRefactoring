namespace BLRefactoring.Shared.Application.Dtos.Trainer;

/// <summary>
/// The new state of a trainer's profile. The whole profile is replaced, so a
/// <see langword="null"/> <see cref="Bio"/> clears the current one.
/// </summary>
public sealed class TrainerEditionRequest
{
    public required string Firstname { get; init; } = null!;
    public required string Lastname { get; init; } = null!;

    /// <summary>
    /// The address at which the trainer wishes to be contacted. Editing it has no
    /// effect on the identity account used to sign in.
    /// </summary>
    public required string ContactEmail { get; init; } = null!;

    public string? Bio { get; init; }
}
