using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.Shared.Application.IntegrationEvents;

/// <summary>
/// A trainer account was created and the transaction committed.
/// </summary>
/// <remarks>
/// Note what crosses and what does not. <see cref="TrainerId"/> stays strongly typed: its only
/// rule is that an identifier cannot be empty, and that will hold for as long as the concept
/// exists, so a message read back in six months still satisfies it. The name and the address
/// arrive as plain strings, because <c>Name</c> and <c>Email</c> carry rules that move —
/// tighten a length limit and every pending message built from the old rule would stop
/// deserialising. Flattening them here is the anti-corruption seam, applied once.
/// </remarks>
public sealed record TrainerRegisteredIntegrationEvent(
    TrainerId TrainerId,
    string Firstname,
    string Lastname,
    string ContactEmail,
    DateTimeOffset OccurredOn) : IIntegrationEvent;

/// <summary>
/// A trainer changed the address they publish, and the transaction committed.
/// </summary>
/// <remarks>
/// Carries both addresses because the point of the notification is to warn the previous one —
/// which cannot be recovered from the aggregate afterwards, since the change already happened.
/// </remarks>
public sealed record TrainerContactEmailChangedIntegrationEvent(
    TrainerId TrainerId,
    string PreviousContactEmail,
    string NewContactEmail,
    DateTimeOffset OccurredOn) : IIntegrationEvent;
