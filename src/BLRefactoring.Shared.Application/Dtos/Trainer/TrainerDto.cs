namespace BLRefactoring.Shared.Application.Dtos.Trainer;

public sealed class TrainerDto
{
    /// <summary>
    /// The version of the aggregate this representation was read at.
    /// </summary>
    /// <remarks>
    /// Carried to the API layer, which publishes it as an <c>ETag</c> and leaves it out of the
    /// response contract. This read model is no longer serialised to callers, so it no longer
    /// needs to say so.
    /// </remarks>
    public byte[] RowVersion { get; init; } = [];

    public required Guid Id { get; init; }
    public required string Firstname { get; init; } = null!;
    public required string Lastname { get; init; } = null!;

    /// <summary>
    /// The address at which the trainer wishes to be contacted, which is not the
    /// email of their identity account.
    /// </summary>
    public required string ContactEmail { get; init; } = null!;

    /// <summary>
    /// The bio of the trainer, or <see langword="null"/> when none was provided.
    /// </summary>
    public string? Bio { get; init; }
}
