using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.Infrastructure;

/// <summary>
/// Entity Framework Core implementation of <see cref="IUnitOfWork"/>, backed by the
/// scoped <see cref="TrainingContext"/> shared with the repositories.
/// </summary>
public sealed class UnitOfWork(TrainingContext trainingContext) : IUnitOfWork
{
    // SQL Server error numbers for a duplicate key: 2601 is a unique index
    // violation, 2627 a unique constraint (primary key included) violation.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await trainingContext.SaveChangesAsync(cancellationToken);
        }
        // Checked before the DbUpdateException branch below: a concurrency failure
        // is a DbUpdateException too, and this is the more specific case.
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "The aggregate was modified by someone else since it was read.",
                ex);
        }
        catch (DbUpdateException ex) when (ex.GetBaseException()
            is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            throw new UniqueConstraintViolationException(
                "The database rejected the changes because a unique constraint was violated.",
                ex);
        }
    }
}
