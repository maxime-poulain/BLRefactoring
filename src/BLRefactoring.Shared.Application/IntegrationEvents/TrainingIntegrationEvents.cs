using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.Shared.Application.IntegrationEvents;

/// <summary>
/// A training was published, and the transaction committed.
/// </summary>
/// <remarks>
/// Creation and edition stay two contracts rather than one carrying a flag. They mean different
/// things to a reader, and a subscriber may well care about one and not the other — the search
/// index treats them alike today, a notification feed would not.
/// </remarks>
public sealed record TrainingPublishedIntegrationEvent(
    TrainingId TrainingId,
    TrainerId TrainerId,
    DateTimeOffset OccurredOn) : IIntegrationEvent;

/// <summary>
/// A published training was modified, and the transaction committed.
/// </summary>
public sealed record TrainingRevisedIntegrationEvent(
    TrainingId TrainingId,
    TrainerId TrainerId,
    DateTimeOffset OccurredOn) : IIntegrationEvent;
