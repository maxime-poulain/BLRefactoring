namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A trainer was deleted — their catalog with them, and their portrait's bytes still to collect.
/// </summary>
/// <remarks>
/// The last domain event to earn its integration event: every other change a trainer's erasure
/// causes already leaves as the per-training deletion facts the cascade raises, which is what
/// clears the search index entry by entry. What no other fact covers is the portrait — bytes in
/// an object store no transaction reaches — so this fact carries the photo's identity, read off
/// the domain event while the aggregate still knew it, and its consumer deletes the bytes with
/// the outbox's retries behind it rather than on a request thread a crash could cut short
/// (ADR 0085).
/// </remarks>
/// <param name="TrainerId">The trainer that was deleted.</param>
/// <param name="PhotoId">Their portrait's identity, when they had one — what the collector deletes.</param>
public sealed record TrainerDeletedIntegrationEvent(Guid TrainerId, Guid? PhotoId) : IIntegrationEvent;
