using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Trainings;

/// <summary>
/// The body of <c>POST /Training</c>.
/// </summary>
/// <remarks>
/// No trainer identifier: a training is always created for the caller, resolved from the
/// <c>trainer_id</c> claim. No training identifier either — the API does not let a client choose
/// the identity of a resource it is creating; each stack mints one on its own side.
/// <para>
/// See <see cref="Trainers.EditTrainerHttpRequest"/> for why the constraints are attributes and
/// what they are, and are not, meant to decide.
/// </para>
/// </remarks>
public sealed class CreateTrainingHttpRequest
{
    /// <summary>
    /// The training's title, as the caller sent it. Bounds are enforced by the value object,
    /// not here.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 5)]
    public string Title { get; init; } = null!;

    /// <summary>
    /// Names of the topics the training covers, resolved against the closed set by the
    /// application layer — which is also what rejects a name that matches nothing. Only the
    /// presence of at least one is a question about the message.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<string> Topics { get; init; } = [];

    /// <summary>
    /// The training's description, as the caller sent it.
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Description { get; init; } = null!;

    /// <summary>
    /// What a participant needs beforehand, as the caller sent it.
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Prerequisites { get; init; } = null!;

    /// <summary>
    /// What a participant leaves with, as the caller sent it.
    /// </summary>
    [Required]
    [StringLength(500)]
    public string AcquiredSkills { get; init; } = null!;
}
