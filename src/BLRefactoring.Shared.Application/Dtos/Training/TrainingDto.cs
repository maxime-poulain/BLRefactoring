namespace BLRefactoring.Shared.Application.Dtos.Training;

/// <summary>
/// A training as the application layer hands it back: already valid, no behaviour attached.
/// </summary>
public sealed class TrainingDto
{
    /// <summary>
    /// The version of the aggregate this representation was read at.
    /// </summary>
    /// <remarks>
    /// Carried to the API layer, which publishes it as an <c>ETag</c> and leaves it out of the
    /// response contract. This read model is no longer serialised to callers, so it no longer
    /// needs to say so.
    /// </remarks>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// The identifier.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// The training's title.
    /// </summary>
    public required string Title { get; set; } = string.Empty;

    /// <summary>
    /// The trainer's identifier.
    /// </summary>
    public required Guid TrainerId { get; set; }

    /// <summary>
    /// The topics the training is filed under.
    /// </summary>
    public required List<string> Topics { get; set; } = [];

    /// <summary>
    /// The training's description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// What a participant needs beforehand.
    /// </summary>
    public required string Prerequisites { get; set; }

    /// <summary>
    /// What a participant leaves with.
    /// </summary>
    public required string AcquiredSkills { get; set; }
}
