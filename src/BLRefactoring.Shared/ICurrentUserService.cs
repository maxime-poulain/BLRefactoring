namespace BLRefactoring.Shared;

public interface ICurrentUserService
{
    public Guid UserId { get; }

    public Guid TrainerId { get; }
}
