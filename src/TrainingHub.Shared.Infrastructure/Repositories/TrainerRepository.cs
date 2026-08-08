using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Domain;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Infrastructure.Pagination;
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

    /// <summary>
    /// Reads one page of trainers, newest first, narrowed by what is given.
    /// </summary>
    /// <remarks>
    /// Every filter is composed onto the queryable <em>before</em> the page is taken, so the count
    /// and the page describe the same set. Filtering a materialised page instead would answer a
    /// short page with a total about a different question — the defect a pager hides best, because
    /// the numbers still look like numbers.
    /// <para>
    /// The term is matched against the two halves of the name and the contact address, and against
    /// nothing else: a trainer is looked up by who they are, and their bio is prose that would turn
    /// a bounded scan into an unbounded one for matches nobody asked for. A blank term is no term,
    /// stated here as well as at the boundary so the method is total on its own.
    /// </para>
    /// </remarks>
    public async Task<PagedResult<Trainer>> GetPageAsync(
        TrainerStatus? status,
        string? search,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        var trainers = trainingContext.Trainers.AsQueryable();

        if (status is not null)
        {
            trainers = trainers.Where(trainer => trainer.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            trainers = trainers.Where(trainer =>
                trainer.Name.Firstname.Contains(term)
                || trainer.Name.Lastname.Contains(term)
                || trainer.ContactEmail.FullAddress.Contains(term));
        }

        return await trainers
            .NewestFirst<Trainer, TrainerId>()
            .ToPagedResultAsync(paging, cancellationToken)
            .ConfigureAwait(false);
    }
}
