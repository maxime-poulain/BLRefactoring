using BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BLRefactoring.Shared.DDD.Infrastructure;

public class TransactionManager : ITransactionManager, IDisposable, IAsyncDisposable
{
    private readonly TrainingContext _trainingContext;
    private IDbContextTransaction? _transaction;

    public TransactionManager(TrainingContext trainingContext)
    {
        _trainingContext = trainingContext;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _transaction = await _trainingContext.Database.BeginTransactionAsync(cancellationToken);

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken);
            _transaction.Dispose();
        }

        _transaction = null;
    }

    public async Task RollBackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            _transaction.Dispose();
        }

        _transaction = null;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
