using System.ComponentModel.DataAnnotations;

namespace BLRefactoring.Shared.Api.Contracts.Trainings;

/// <summary>
/// The body of <c>PUT /Training/{trainingId}</c>: the new state of the training.
/// </summary>
/// <remarks>
/// The training being edited travels in the route and the expected version in <c>If-Match</c>;
/// neither belongs in the body. The CQRS <c>EditTrainingCommand</c> used to hold both as
/// <c>[JsonIgnore]</c> properties with public setters, assigned by the controller after model
/// binding — the mapping now composes them explicitly instead.
/// <para>
/// See <see cref="Trainers.EditTrainerRequestHttp"/> for why the constraints are attributes and
/// what they are, and are not, meant to decide.
/// </para>
/// </remarks>
public sealed class EditTrainingRequestHttp
{
    [Required]
    [StringLength(100, MinimumLength = 5)]
    public string Title { get; init; } = null!;

    /// <summary>
    /// The complete set of topics after the edit: topics are replaced, not merged.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<string> Topics { get; init; } = [];

    [Required]
    [StringLength(500)]
    public string Description { get; init; } = null!;

    [Required]
    [StringLength(500)]
    public string Prerequisites { get; init; } = null!;

    [Required]
    [StringLength(500)]
    public string AcquiredSkills { get; init; } = null!;
}
