namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A trainer was placed under sanction — the fact, as published to consumers outside this bounded
/// context.
/// </summary>
/// <remarks>
/// The reason travels with the fact because it is part of it: the sanction and its motive were
/// decided together, and a consumer that had to look the motive up could read one the trainer has
/// since been cleared of. The address the notice is sent to does not travel, for the opposite
/// reason — it is not part of the sanction (ADR 0056).
/// <para>
/// No list of trainings, because the suspension wrote to none. The catalogue leaves public view by
/// being derived from its owner's standing, which is what lets the whole reaction be one call
/// rather than one per training (ADR 0050).
/// </para>
/// </remarks>
/// <param name="TrainerId">The identifier of the suspended trainer.</param>
/// <param name="Reason">Why they were suspended.</param>
public sealed record TrainerSuspendedIntegrationEvent(
    Guid TrainerId,
    string Reason) : IIntegrationEvent;
