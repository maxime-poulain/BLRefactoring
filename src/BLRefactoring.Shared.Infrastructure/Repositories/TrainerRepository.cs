using BLRefactoring.Shared.Common.Specifications;
using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Infrastructure.Specifications;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.Infrastructure.Repositories;

/// <summary>
/// Reads and writes trainers through EF Core.
/// </summary>
public sealed class TrainerRepository(TrainingContext trainingContext) : ITrainerRepository
{
    /// <summary>
    /// Finds a trainer by identifier, or <see langword="null"/> when there is none.
    /// </summary>
    public async Task<Trainer?> GetByIdAsync(
        TrainerId id,
        CancellationToken cancellationToken = default)
    {
        return await trainingContext.Trainers
            .FirstOrDefaultAsync(trainer => trainer.Id == id, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Finds the trainer behind an identity account, or <see langword="null"/> when there is none.
    /// </summary>
    public async Task<Trainer?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        return await trainingContext.Trainers
            .FirstOrDefaultAsync(trainer => trainer.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Answers whether a trainer with this identifier exists, without loading it.
    /// </summary>
    public Task<bool> ExistsAsync(TrainerId id, CancellationToken cancellationToken = default)
    {
        return trainingContext.Trainers.AnyAsync(trainer => trainer.Id == id, cancellationToken);
    }

    /// <summary>
    /// Registers a new trainer to be written when the unit of work commits.
    /// </summary>
    public void Add(Trainer trainer)
    {
        trainingContext.Trainers.Add(trainer);
    }

    // Marking the aggregate modified guarantees an UPDATE on the Trainer row, and
    // therefore a check of its concurrency token, whatever part of the aggregate the
    // edition actually touched.

    /// <summary>
    /// Marks a trainer as changed.
    /// </summary>
    public void Update(Trainer trainer)
    {
        trainingContext.Trainers.Update(trainer);
    }

    /// <summary>
    /// Registers a trainer for removal.
    /// </summary>
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
