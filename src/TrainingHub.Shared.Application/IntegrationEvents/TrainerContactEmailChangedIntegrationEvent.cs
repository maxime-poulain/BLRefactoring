namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A trainer's contact address changed — the fact, as published to consumers outside this bounded
/// context.
/// </summary>
/// <remarks>
/// Both addresses travel with the fact because the reaction that motivates it — warning the address
/// being left behind, once the delivery worker exists — needs the old one, and the aggregate has
/// already forgotten it by the time any consumer runs.
/// </remarks>
/// <param name="TrainerId">The identifier of the trainer whose contact address changed.</param>
/// <param name="OldContactEmail">The address the trainer moved away from.</param>
/// <param name="NewContactEmail">The address the trainer moved to.</param>
/// <param name="Language">
/// The language the recipient reads in, as the account stated it, or <see langword="null"/> when
/// it stated none. It rides the fact for the reason the address does: whoever receives this notice
/// is resolved here and nowhere else, so their language is resolved here too (ADR 0091).
/// </param>
public sealed record TrainerContactEmailChangedIntegrationEvent(
    Guid TrainerId,
    string OldContactEmail,
    string NewContactEmail,
    string? Language) : IIntegrationEvent;
