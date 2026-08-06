using TrainingHub.Shared.Application.Queries;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace TrainingHub.Shared.Infrastructure.Queries;

/// <inheritdoc />
public sealed class TrainingOwnerQuery(TrainingContext trainingContext) : ITrainingOwnerQuery
{
    /// <inheritdoc />
    /// <remarks>
    /// An empty identifier names no training, and answering so is this method's job rather than
    /// throwing. It reads a <see cref="Guid"/> off a route and turns it into a domain identifier,
    /// which makes it the layer that has to cope with a primitive that is not one —
    /// <c>TrainingId.Create</c> refuses <see cref="Guid.Empty"/>, by design.
    /// <para>
    /// It matters because of who calls this first. <c>TrainingOwnerAuthorizationHandler</c> runs
    /// before the action, so every guarded command route reached this line before its validation
    /// pipeline saw the request: a caller sending <c>Guid.Empty</c> got a 500 out of the
    /// authorization stage, not the 400 the pipeline was there to answer. Guarding in the handler
    /// instead would have had to choose between failing the requirement — turning a malformed
    /// request into a 403 — and duplicating this knowledge one layer up.
    /// </para>
    /// </remarks>
    public async Task<Guid?> GetOwnerIdAsync(Guid trainingId, CancellationToken cancellationToken = default)
    {
        if (trainingId == Guid.Empty)
        {
            return null;
        }

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
