namespace TrainingHub.Shared.Api.Contracts.Trainings;

/// <summary>
/// A training as the API publishes it.
/// </summary>
/// <remarks>
/// The application layer's <c>TrainingDto</c> is a read model shared by both stacks; this is the
/// contract callers depend on. As with <see cref="Trainers.TrainerHttpResponse"/>, the row
/// version is absent here and travels in the <c>ETag</c> header.
/// </remarks>
public sealed class TrainingHttpResponse
{
    /// <summary>
    /// The identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The training's title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The trainer who owns the training, and the only one allowed to edit or delete it.
    /// </summary>
    public required Guid TrainerId { get; init; }

    /// <summary>
    /// The topics the training is filed under.
    /// </summary>
    public required List<string> Topics { get; init; }

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
    /// Whether the training is offered to the public or withdrawn from it: <c>Published</c> or
    /// <c>Unpublished</c>.
    /// </summary>
    /// <remarks>
    /// Published as the word rather than a boolean, so that adding a state later is an additive
    /// change to this contract instead of a breaking one. It says what the owner decided, not what
    /// a visitor would see: that is composed with the owner's standing, which this response does
    /// not carry because the only caller who can read it is the owner themselves (ADR 0050).
    /// </remarks>
    public required string Status { get; init; }
}
