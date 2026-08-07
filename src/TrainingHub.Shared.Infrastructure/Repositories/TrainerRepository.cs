using TrainingHub.Shared.Domain;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace TrainingHub.Shared.Infrastructure.Repositories;

/// <summary>
/// Reads and writes trainers through EF Core.
/// </summary>
public sealed class TrainerRepository(TrainingContext trainingContext) : ITrainerRepository, ITrainerStanding
{
    /// <summary>
    /// Answers whether the given trainer is under sanction, without loading the aggregate.
    /// </summary>
    /// <remarks>
    /// One column, asked of the trainer's own table, for a decision that belongs to the training
    /// aggregate: the port is declared beside <see cref="Training"/> and implemented here, where
    /// the row is. A trainer no row answers to reports <see langword="false"/> — this question is
    /// about standing, and "no such trainer" is a different refusal with a different code.
    /// </remarks>
    public Task<bool> IsSuspendedAsync(TrainerId trainerId, CancellationToken cancellationToken = default)
    {
        var suspended = TrainerStatus.Suspended;

        return trainingContext.Trainers
            .AnyAsync(trainer => trainer.Id == trainerId && trainer.Status == suspended, cancellationToken);
    }

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
}
