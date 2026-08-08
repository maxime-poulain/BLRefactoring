using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Application.Queries;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;

namespace TrainingHub.Shared.Infrastructure.Queries;

/// <inheritdoc />
public sealed class TrainerStandingQuery(TrainingContext trainingContext) : ITrainerStandingQuery
{
    /// <inheritdoc />
    /// <remarks>
    /// An empty identifier names no trainer, and answering so is this method's job rather than
    /// throwing: <c>TrainerId.Create</c> refuses <see cref="Guid.Empty"/>, and this runs before the
    /// validation pipeline that exists to turn a malformed request into a <c>400</c>. The same trap
    /// <c>TrainingOwnerQuery</c> met, on the same path, for the same reason.
    /// </remarks>
    public async Task<bool> IsSuspendedAsync(
        Guid trainerId,
        CancellationToken cancellationToken = default)
    {
        if (trainerId == Guid.Empty)
        {
            return false;
        }

        var id = TrainerId.Create(trainerId);

        // AnyAsync over the pair rather than reading the trainer and comparing in memory: the
        // question is a bit, the answer is an index seek, and nothing here needs the aggregate.
        return await trainingContext.Trainers
            .AsNoTracking()
            .AnyAsync(
                trainer => trainer.Id == id && trainer.Status == TrainerStatus.Suspended,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
