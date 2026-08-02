using BLRefactoring.Shared.Application.Queries;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.Infrastructure.Queries;

/// <inheritdoc />
public sealed class TrainingOwnerQuery(TrainingContext trainingContext) : ITrainingOwnerQuery
{
    /// <inheritdoc />
    public async Task<Guid?> GetOwnerIdAsync(Guid trainingId, CancellationToken cancellationToken = default)
    {
        var id = TrainingId.Create(trainingId);

        // Projected into an anonymous type rather than selected as a Guid, so that "no such
        // training" comes back as a null reference instead of Guid.Empty — which is a valid-looking
        // identifier no caller could tell apart from a real one.
        var owner = await trainingContext.Trainings
            .Where(training => training.Id == id)
            .Select(training => new { TrainerId = training.TrainerId.Value })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return owner?.TrainerId;
    }
}
