using System.Diagnostics.CodeAnalysis;
using TrainingHub.Shared.Common;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

/// <summary>
/// Identifies a published portrait.
/// </summary>
/// <remarks>
/// Fresh on every upload, and never reused: it is what makes a photo's address immutable, so a
/// replacement writes a new key rather than overwriting one a cache may still be serving
/// (ADR 0021). It sits beside <see cref="TrainerId"/> in the object key
/// <c>trainers/{trainerId}/{photoId}</c> — which is exactly why it is typed. Two bare
/// <see cref="Guid"/>s in one string are two chances to write them in the wrong order, and nothing
/// would have said so.
/// </remarks>
public sealed class PhotoId : EntityId<PhotoId>
{
    [SuppressMessage("Style", "IDE0051:Remove unused private members",
        Justification = "EntityId<T>.BuildFactory resolves this constructor with GetConstructor(..., NonPublic) and compiles it into the factory every Create and Generate call goes through. It is the only way an identifier is ever built; the analyzer cannot see a call that a compiled expression tree makes.")]
    private PhotoId(Guid value) : base(value)
    {
    }
}
