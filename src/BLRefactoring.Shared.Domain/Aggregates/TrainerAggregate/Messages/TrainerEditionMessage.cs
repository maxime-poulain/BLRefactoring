namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.Messages;

public class TrainerEditionMessage
{
    public required string Firstname { get; init; }
    public required string Lastname { get; init; }

    /// <summary>
    /// The address at which the trainer wishes to be contacted. Distinct from the
    /// email of the identity account, which is never edited through the profile.
    /// </summary>
    public required string ContactEmail { get; init; }

    /// <summary>
    /// The bio of the trainer. <see langword="null"/> means no bio: at creation time
    /// none was provided, on edition the existing one is cleared. A provided value
    /// (even empty) is validated by the <c>Bio</c> value object.
    /// </summary>
    public string? Bio { get; init; }
}
