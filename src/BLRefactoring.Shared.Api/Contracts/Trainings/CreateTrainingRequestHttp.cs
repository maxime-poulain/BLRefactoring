namespace BLRefactoring.Shared.Api.Contracts.Trainings;

/// <summary>
/// The body of <c>POST /Training</c>.
/// </summary>
/// <remarks>
/// No trainer identifier: a training is always created for the caller, resolved from the
/// <c>trainer_id</c> claim. No training identifier either — the API does not let a client choose
/// the identity of a resource it is creating; each stack mints one on its own side.
/// <para>
/// See <see cref="Trainers.EditTrainerRequestHttp"/> for why no property is <c>required</c>.
/// </para>
/// </remarks>
public sealed class CreateTrainingRequestHttp
{
    public string Title { get; init; } = null!;

    /// <summary>
    /// Names of the topics the training covers, resolved against the closed set by the
    /// application layer.
    /// </summary>
    public List<string> Topics { get; init; } = [];

    public string Description { get; init; } = null!;

    public string Prerequisites { get; init; } = null!;

    public string AcquiredSkills { get; init; } = null!;
}
