namespace BLRefactoring.Shared.Application.Dtos.Training;

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

    public required Guid Id { get; set; }
    public required string Title { get; set; } = string.Empty;
    public required Guid TrainerId { get; set; }
    public required List<string> Topics { get; set; } = [];
    public required string Description { get; set; }
    public required string Prerequisites { get; set; }
    public required string AcquiredSkills { get; set; }
}
