using BLRefactoring.DDD.Infrastructure.Repositories.EfCore;
using BLRefactoring.Shared;
using Microsoft.EntityFrameworkCore.Storage;

namespace BLRefactoring.DDD.Infrastructure;

public class TransactionManager : ITransactionManager
{
    private readonly TrainingContext _trainingContext;
    private IDbContextTransaction? _transaction;

    public TransactionManager(TrainingContext trainingContext)
    {
        _trainingContext = trainingContext;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        _transaction = await _trainingContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken);
        }

        _transaction = null;
    }

    public async Task RollBackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
        }

        _transaction = null;
    }
}
