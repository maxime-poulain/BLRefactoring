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
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    /// <summary>
    /// The trainer who owns the training, and the only one allowed to edit or delete it.
    /// </summary>
    public required Guid TrainerId { get; init; }

    public required List<string> Topics { get; init; }

    public required string Description { get; init; }

    public required string Prerequisites { get; init; }

    public required string AcquiredSkills { get; init; }
}
