using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Application.Catalogue;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
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
public sealed class CatalogueDetailQuery(
    TrainingContext trainingContext,
    ITrainerPhotoStore photoStore)
    : ICatalogueDetailQuery
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
                    .FirstOrDefault(),
                // The same condition the portrait itself is served under, and it has to be here too:
                // a page that offers an address the endpoint will answer 404 renders a broken image
                // rather than no image (ADR 0063).
                TrainerPhotoId = trainingContext.Trainers
                    .Where(trainer => trainer.Id == candidate.TrainerId
                        && trainer.Photo!.SanitisedOnUtc != null)
                    .Select(trainer => trainer.Photo!.PhotoId)
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
            AcquiredSkills = detail.AcquiredSkills,
            TrainerPhotoId = detail.TrainerPhotoId?.Value
        };
    }

    /// <inheritdoc />
    public async Task<TrainerPhotoDto?> FindOfferedPortraitAsync(
        Guid trainingId,
        Guid photoId,
        CancellationToken cancellationToken = default)
    {
        // The same first statement as the detail, and for the same reason: what a visitor may see is
        // composed in the index and nowhere here. A portrait is content of the training's page, so
        // it inherits the training's visibility rather than acquiring one of its own.
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

        var owner = await trainingContext.Trainings
            .AsNoTracking()
            .Where(candidate => candidate.Id == TrainingId.Create(trainingId))
            .Select(candidate => candidate.TrainerId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (owner is null)
        {
            return null;
        }

        // Two conditions the address cannot satisfy on its own. The identity must be the one the
        // owner has now — an old photo's address stops resolving the moment it is replaced, which is
        // what makes the response cacheable forever — and the stamp must be there.
        //
        // Written as `SanitisedOnUtc != null` rather than through the domain's own MayBePublished:
        // that is a computed property on a value object inside a complex property, which EF has
        // nothing to translate. ADR 0028 says a specification is one expression answering in memory
        // and as query criteria; this predicate cannot be one, so it is stated here rather than
        // dressed up as something the domain owns.
        var photo = PhotoId.Create(photoId);

        var portrait = await trainingContext.Trainers
            .AsNoTracking()
            .Where(trainer => trainer.Id == owner
                && trainer.Photo!.PhotoId == photo
                && trainer.Photo.SanitisedOnUtc != null)
            .Select(trainer => new { trainer.Photo!.ContentType })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (portrait is null)
        {
            return null;
        }

        var stored = await photoStore.FetchAsync(owner, photo, cancellationToken).ConfigureAwait(false);

        // A row naming bytes the store does not hold answers "no portrait" rather than an error,
        // exactly as the two authenticated readers do. The media type comes from the column rather
        // than from the store's echo of it: that is the one the aggregate vetted.
        return stored is null
            ? null
            : new TrainerPhotoDto(photoId, stored.Content, portrait.ContentType);
    }
}
