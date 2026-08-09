using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Application.Catalogue;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;

namespace TrainingHub.Shared.Infrastructure.Search;

/// <inheritdoc />
/// <remarks>
/// Two statements, and the order is the design: the index is asked whether the training is on offer,
/// and only then is the write model asked what it says. A single query joining both would read the
/// same rows, and would also make it possible to write a visibility predicate here — which is the
/// one thing this adapter must never do (ADR 0062).
/// <para>
/// It lives beside the search adapter because it is the same context's storage it opens, and
/// nowhere else may name those tables (ADR 0059).
/// </para>
/// </remarks>
public sealed class CatalogueDetailQuery(TrainingContext trainingContext) : ICatalogueDetailQuery
{
    /// <inheritdoc />
    public async Task<CatalogueTrainingDetailDto?> FindOfferedAsync(
        Guid trainingId,
        CancellationToken cancellationToken = default)
    {
        // The index decides. "On offer" is composed by the nine consumers that maintain this table
        // and by nothing here, so this adapter asks whether an entry exists and never why.
        var isOffered = await trainingContext.Set<TrainingSearchEntry>()
            .AsNoTracking()
            .AnyAsync(
                entry => entry.TrainingId == trainingId && entry.IsPublished && !entry.IsTrainerHidden,
                cancellationToken)
            .ConfigureAwait(false);

        if (!isOffered)
        {
            return null;
        }

        // The write model says what the training is. Columns rather than the aggregate, as every
        // read on this side does — and the trainer's name with them, read now rather than stored,
        // because no fact carries a rename to an index that could hold one.
        var id = TrainingId.Create(trainingId);

        var detail = await trainingContext.Trainings
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new
            {
                Title = candidate.Title.Value,
                Description = candidate.Description.Value,
                Prerequisites = candidate.Prerequisites.Value,
                AcquiredSkills = candidate.AcquiredSkills.Value,
                Topics = candidate.Topics.Select(topic => topic.Name).ToList(),
                TrainerName = trainingContext.Trainers
                    .Where(trainer => trainer.Id == candidate.TrainerId)
                    .Select(trainer => trainer.Name.Firstname + " " + trainer.Name.Lastname)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // The index said yes and the row is gone: the entry is a moment stale, which is what an
        // eventually consistent read model is. Answering nothing is the same answer a visitor would
        // have got a second later.
        if (detail is null)
        {
            return null;
        }

        return new CatalogueTrainingDetailDto
        {
            Id = trainingId,
            Title = detail.Title,
            // An owner whose account is gone leaves the name empty rather than dropping the
            // training, the reading the administrative listing already gives the same situation.
            TrainerName = detail.TrainerName ?? string.Empty,
            Topics = detail.Topics,
            Description = detail.Description,
            Prerequisites = detail.Prerequisites,
            AcquiredSkills = detail.AcquiredSkills
        };
    }
}
