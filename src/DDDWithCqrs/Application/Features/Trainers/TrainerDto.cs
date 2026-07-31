using System.Text.Json.Serialization;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers;

public class TrainerDto
{
    /// <summary>
    /// The version of the aggregate this representation was read at.
    /// </summary>
    /// <remarks>
    /// Carried to the controller so it can emit an <c>ETag</c>, and deliberately
    /// kept out of the JSON body: the version is a transport concern, not part of
    /// the business contract.
    /// </remarks>
    [JsonIgnore] public byte[] RowVersion { get; init; } = [];

    public Guid Id { get; init; }
    public string Firstname { get; init; } = null!;
    public string Lastname { get; init; } = null!;

    /// <summary>
    /// The address at which the trainer wishes to be contacted, which is not the
    /// email of their identity account.
    /// </summary>
    public string ContactEmail { get; init; } = null!;

    /// <summary>
    /// The bio of the trainer, or <see langword="null"/> when none was provided.
    /// </summary>
    public string? Bio { get; init; }
}
