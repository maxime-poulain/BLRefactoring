namespace BLRefactoring.Shared.Api.Contracts.Trainings;

/// <summary>
/// The body of <c>PUT /Training/{trainingId}</c>: the new state of the training.
/// </summary>
/// <remarks>
/// The training being edited travels in the route and the expected version in <c>If-Match</c>;
/// neither belongs in the body. The CQRS <c>EditTrainingCommand</c> used to hold both as
/// <c>[JsonIgnore]</c> properties with public setters, assigned by the controller after model
/// binding — the mapping now composes them explicitly instead.
/// </remarks>
public sealed class EditTrainingRequestHttp
{
    public string Title { get; init; } = null!;

    /// <summary>
    /// The complete set of topics after the edit: topics are replaced, not merged.
    /// </summary>
    public List<string> Topics { get; init; } = [];

    public string Description { get; init; } = null!;

    public string Prerequisites { get; init; } = null!;

    public string AcquiredSkills { get; init; } = null!;
}
