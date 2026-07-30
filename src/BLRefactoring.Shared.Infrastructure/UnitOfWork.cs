using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;

namespace BLRefactoring.Shared.Infrastructure;

/// <summary>
/// Entity Framework Core implementation of <see cref="IUnitOfWork"/>, backed by the
/// scoped <see cref="TrainingContext"/> shared with the repositories.
/// </summary>
public sealed class UnitOfWork(TrainingContext trainingContext) : IUnitOfWork
{
    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => trainingContext.SaveChangesAsync(cancellationToken);
}
