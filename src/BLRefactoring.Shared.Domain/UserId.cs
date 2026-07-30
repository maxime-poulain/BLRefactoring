using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain;

public sealed class UserId : EntityId<UserId>
{
    private UserId(Guid value) : base(value)
    {
    }
}
