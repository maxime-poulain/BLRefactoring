using BLRefactoring.Shared.Common.Specifications;
using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Infrastructure.Specifications;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.Infrastructure.Repositories;

public sealed class TrainerRepository(TrainingContext trainingContext) : ITrainerRepository
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

    public Task<bool> ExistsAsync(TrainerId id, CancellationToken cancellationToken = default)
    {
        return trainingContext.Trainers.AnyAsync(trainer => trainer.Id == id, cancellationToken);
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
        // No Include: owned types are part of the entity and come back with it. Naming two of
        // the three here — Name and ContactEmail, but not Bio — read like a deliberate partial
        // load, which it never was.
        return trainingContext.Trainers.ToListAsync(cancellationToken);
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
    public async Task<bool> AnyAsync(ISpecification<Trainer> spec, CancellationToken cancellationToken = default)
    {
        return await SpecificationEvaluator.GetQuery(trainingContext.Trainers, spec)
            .AnyAsync(cancellationToken);
    }
}
