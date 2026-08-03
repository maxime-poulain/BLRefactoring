using System.Diagnostics.CodeAnalysis;
using TrainingHub.Shared.Common;

namespace TrainingHub.Shared.Domain;

/// <summary>
/// Identifies an identity account.
/// </summary>
public sealed class UserId : EntityId<UserId>
{
    [SuppressMessage("Style", "IDE0051:Remove unused private members",
        Justification = "EntityId<T>.BuildFactory resolves this constructor with GetConstructor(..., NonPublic) and compiles it into the factory every Create and Generate call goes through. It is the only way an identifier is ever built; the analyzer cannot see a call that a compiled expression tree makes.")]
    private UserId(Guid value) : base(value)
    {
    }
}
