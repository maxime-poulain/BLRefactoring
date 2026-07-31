using System.Text.Json.Serialization;

namespace BLRefactoring.Shared.Application.Dtos.Training;

public class TrainingDto
{
    /// <summary>
    /// The version of the aggregate this representation was read at.
    /// </summary>
    /// <remarks>
    /// Carried to the controller so it can emit an <c>ETag</c>, and deliberately
    /// kept out of the JSON body: the version is a transport concern, not part of
    /// the business contract.
    /// </remarks>
    [JsonIgnore] public byte[] RowVersion { get; set; } = [];

    public required Guid Id { get; set; }
    public required string Title { get; set; } = string.Empty;
    public required Guid TrainerId { get; set; }
    public required List<string> Topics { get; set; } = [];
    public required string Description { get; set; }
    public required string Prerequisites { get; set; }
    public required string AcquiredSkills { get; set; }
}
