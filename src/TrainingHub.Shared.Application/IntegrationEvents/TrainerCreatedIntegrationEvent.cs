namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A trainer was created — the fact, as published to consumers outside this bounded context.
/// </summary>
/// <remarks>
/// The payload is the domain event flattened to primitives: consumers must be able to deserialize
/// the fact without referencing the domain model, its value objects or its typed identifiers. The
/// name and the contact address travel with the fact for the same reason they travel on the domain
/// event — a consumer reacting later (the welcome email, once the delivery worker exists) works
/// from the message alone, never by loading the aggregate.
/// </remarks>
/// <param name="TrainerId">The identifier of the created trainer.</param>
/// <param name="Firstname">The trainer's first name.</param>
/// <param name="Lastname">The trainer's last name.</param>
/// <param name="ContactEmail">The address the trainer wishes to be contacted at.</param>
/// <param name="Language">
/// The language the recipient reads in, as the account stated it, or <see langword="null"/> when
/// it stated none. It rides the fact for the reason the address does: whoever receives this notice
/// is resolved here and nowhere else, so their language is resolved here too (ADR 0091).
/// </param>
public sealed record TrainerCreatedIntegrationEvent(
    Guid TrainerId,
    string Firstname,
    string Lastname,
    string ContactEmail,
    string? Language) : IIntegrationEvent;
