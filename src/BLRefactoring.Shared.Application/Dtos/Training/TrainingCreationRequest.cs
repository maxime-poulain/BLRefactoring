
namespace BLRefactoring.Shared.Application.Dtos.Training;

/// <summary>
/// What the application layer needs to create a training, before any of it has been
/// turned into value objects.
/// </summary>
public sealed class TrainingCreationRequest
{
    /// <summary>
    /// The training's title, as the caller sent it. Bounds are enforced by the value object,
    /// not here.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The topic names to file the training under. Unknown names are a validation error, not
    /// an exception — topics are a closed set the domain owns.
    /// </summary>
    public required List<string> Topics { get; init; } = [];

    /// <summary>
    /// The training's description, as the caller sent it.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// What a participant needs beforehand, as the caller sent it.
    /// </summary>
    public required string Prerequisites { get; init; }

    /// <summary>
    /// What a participant leaves with, as the caller sent it.
    /// </summary>
    public required string AcquiredSkills { get; init; }
}

/// <summary>
/// What the application layer needs to replace a training's details.
/// </summary>
public sealed class TrainingEditionRequest
{
    /// <summary>
    /// The training's title, as the caller sent it. Bounds are enforced by the value object,
    /// not here.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The topic names to file the training under. Unknown names are a validation error, not
    /// an exception — topics are a closed set the domain owns.
    /// </summary>
    public required List<string> Topics { get; init; } = [];

    /// <summary>
    /// The training's description, as the caller sent it.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// What a participant needs beforehand, as the caller sent it.
    /// </summary>
    public required string Prerequisites { get; init; }

    /// <summary>
    /// What a participant leaves with, as the caller sent it.
    /// </summary>
    public required string AcquiredSkills { get; init; }
}
