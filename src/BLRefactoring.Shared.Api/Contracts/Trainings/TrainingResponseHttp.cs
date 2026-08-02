namespace BLRefactoring.Shared.Api.Contracts.Trainings;

/// <summary>
/// A training as the API publishes it.
/// </summary>
/// <remarks>
/// The application layer's <c>TrainingDto</c> is a read model shared by both stacks; this is the
/// contract callers depend on. As with <see cref="Trainers.TrainerResponseHttp"/>, the row
/// version is absent here and travels in the <c>ETag</c> header.
/// </remarks>
public sealed class TrainingResponseHttp
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
}
