using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BLRefactoring.Shared.Infrastructure;

public class TransactionManager(TrainingContext trainingContext)
    : ITransactionManager, IDisposable, IAsyncDisposable
{
    private IDbContextTransaction? _transaction;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _transaction = await trainingContext.Database.BeginTransactionAsync(cancellationToken);

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
