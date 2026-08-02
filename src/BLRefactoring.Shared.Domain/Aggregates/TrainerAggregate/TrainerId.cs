using System.Diagnostics.CodeAnalysis;
using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

/// <summary>
/// Identifies a trainer.
/// </summary>
public sealed class TrainerId : EntityId<TrainerId>
{
    [SuppressMessage("Style", "IDE0051:Remove unused private members",
        Justification = "EntityId<T>.BuildFactory resolves this constructor with GetConstructor(..., NonPublic) and compiles it into the factory every Create and Generate call goes through. It is the only way an identifier is ever built; the analyzer cannot see a call that a compiled expression tree makes.")]
    private TrainerId(Guid value) : base(value)
    {
    }
}
