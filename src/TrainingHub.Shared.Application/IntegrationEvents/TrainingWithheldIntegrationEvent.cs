namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A training was taken out of public view by the administration — the fact, as published to
/// consumers outside this bounded context.
/// </summary>
/// <remarks>
/// The title travels with the fact, which is the exception this API makes to announcing identifiers
/// only, and it is made on purpose: the one consumer waiting has to name the training to the person
/// it was taken from, and an owner with a dozen trainings told that "a training" was withheld has
/// been told nothing. It is the title as it stood when the decision was taken, which is also the
/// title the administrator saw.
/// </remarks>
/// <param name="TrainingId">The identifier of the withheld training.</param>
/// <param name="TrainerId">The identifier of the trainer who owns it.</param>
/// <param name="Title">The training's title, as it stood when it was withheld.</param>
/// <param name="Reason">Why it was withheld.</param>
public sealed record TrainingWithheldIntegrationEvent(
    Guid TrainingId,
    Guid TrainerId,
    string Title,
    string Reason) : IIntegrationEvent;
