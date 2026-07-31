using BLRefactoring.Shared.Common.Specifications;
using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Infrastructure.Specifications;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.Infrastructure.Repositories;

public class TrainerRepository(TrainingContext trainingContext) : ITrainerRepository
{
    public async Task<Trainer?> GetByIdAsync(
        TrainerId id,
        CancellationToken cancellationToken = default)
    {
        return await trainingContext.Trainers
            .FirstOrDefaultAsync(trainer => trainer.Id == id, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Trainer?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        return await trainingContext.Trainers
            .FirstOrDefaultAsync(trainer => trainer.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public void Add(Trainer trainer)
    {
        trainingContext.Trainers.Add(trainer);
    }

    // Marking the aggregate modified guarantees an UPDATE on the Trainer row, and
    // therefore a check of its concurrency token, whatever part of the aggregate the
    // edition actually touched.
    public void Update(Trainer trainer)
    {
        trainingContext.Trainers.Update(trainer);
    }

    public Task<List<Trainer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Owned Entities are by default included in the query.
        // However, we are explicitly including them here for the sake of clarity.
        return trainingContext.Trainers
            .Include(trainer => trainer.ContactEmail)
            .Include(trainer => trainer.Name)
            .ToListAsync(cancellationToken);
    }

    public void Delete(Trainer trainer)
    {
        trainingContext.Trainers.Remove(trainer);
    }

    /// <inheritdoc />
    public async Task<List<Trainer>> GetAsync(ISpecification<Trainer> spec, CancellationToken cancellationToken = default)
    {
        return await SpecificationEvaluator.GetQuery(trainingContext.Trainers, spec)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Trainer?> FirstOrDefaultAsync(ISpecification<Trainer> spec, CancellationToken cancellationToken = default)
    {
        return await SpecificationEvaluator.GetQuery(trainingContext.Trainers, spec)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> AnyAsync(ISpecification<Trainer> spec, CancellationToken cancellationToken = default)
    {
        return await SpecificationEvaluator.GetQuery(trainingContext.Trainers, spec)
            .AnyAsync(cancellationToken);
    }
}
