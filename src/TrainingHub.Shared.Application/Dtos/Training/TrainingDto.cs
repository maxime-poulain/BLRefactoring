namespace TrainingHub.Shared.Application.Dtos.Training;

/// <summary>
/// A training as the application layer hands it back: already valid, no behavior attached.
/// </summary>
public sealed class TrainingDto
{
    /// <summary>
    /// The version of the aggregate this representation was read at.
    /// </summary>
    /// <remarks>
    /// Carried to the API layer, which publishes it as an <c>ETag</c> and leaves it out of the
    /// response contract. This read model is no longer serialized to callers, so it no longer
    /// needs to say so.
    /// </remarks>
    public byte[] RowVersion { get; init; } = [];

    /// <summary>
    /// The identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The training's title.
    /// </summary>
    public required string Title { get; init; } = string.Empty;

    /// <summary>
    /// The trainer's identifier.
    /// </summary>
    public required Guid TrainerId { get; init; }

    /// <summary>
    /// The topics the training is filed under.
    /// </summary>
    public required List<string> Topics { get; init; } = [];

    /// <summary>
    /// The training's description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// What a participant needs beforehand.
    /// </summary>
    public required string Prerequisites { get; init; }

    /// <summary>
    /// What a participant leaves with.
    /// </summary>
    public required string AcquiredSkills { get; init; }

    /// <summary>
    /// Whether the training is offered to the public or withdrawn from it.
    /// </summary>
    /// <remarks>
    /// The word the domain uses — <c>Published</c> or <c>Unpublished</c> — rather than a boolean:
    /// this is one half of a lifecycle that can grow, and a flag would have to be renamed the day
    /// it does. What the public can actually see is composed with the owner's standing and is not
    /// this field (ADR 0050).
    /// </remarks>
    public required string Status { get; init; }

    /// <summary>
    /// Why the training was withheld, or <see langword="null"/> when it was not.
    /// </summary>
    /// <remarks>
    /// Present if and only if <see cref="Status"/> is <c>Withheld</c>. Its owner reads it here:
    /// a state the interface renders as merely "not published" would hide the one thing they need
    /// to know (ADR 0052, ADR 0057).
    /// </remarks>
    public string? WithholdingReason { get; init; }
}
