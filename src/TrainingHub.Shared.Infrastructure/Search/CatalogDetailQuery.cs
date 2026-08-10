using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Infrastructure.Pagination;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;

namespace TrainingHub.Shared.Infrastructure.Search;

/// <inheritdoc />
/// <remarks>
/// Two statements, and the order is the design: the index is asked whether the training is on offer
/// — or, for a profile, whether the person is offering anything — and only then is the write model
/// asked what it says. A single query joining both would read the same rows, and would also make it
/// possible to write a visibility predicate here — which is the one thing this adapter must never
/// do (ADR 0062, ADR 0070).
/// <para>
/// It lives beside the search adapter because it is the same context's storage it opens, and
/// nowhere else may name those tables (ADR 0059).
/// </para>
/// </remarks>
public sealed class CatalogDetailQuery(
    TrainingContext trainingContext,
    ITrainerPhotoStore photoStore)
    : ICatalogDetailQuery
{
    /// <inheritdoc />
    public async Task<CatalogTrainingDetailDto?> FindOfferedAsync(
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
                candidate.TrainerId,
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
                        && trainer.Photo!.SanitizedOnUtc != null)
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

        return new CatalogTrainingDetailDto
        {
            Id = trainingId,
            Title = detail.Title,
            // An owner whose account is gone leaves the name empty rather than dropping the
            // training, the reading the administrative listing already gives the same situation.
            TrainerName = detail.TrainerName ?? string.Empty,
            TrainerId = detail.TrainerId.Value,
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
        // Written as `SanitizedOnUtc != null` rather than through the domain's own MayBePublished:
        // that is a computed property on a value object inside a complex property, which EF has
        // nothing to translate. ADR 0028 says a specification is one expression answering in memory
        // and as query criteria; this predicate cannot be one, so it is stated here rather than
        // dressed up as something the domain owns.
        var photo = PhotoId.Create(photoId);

        var portrait = await trainingContext.Trainers
            .AsNoTracking()
            .Where(trainer => trainer.Id == owner
                && trainer.Photo!.PhotoId == photo
                && trainer.Photo.SanitizedOnUtc != null)
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

    /// <inheritdoc />
    public async Task<CatalogTrainerDto?> FindOfferedTrainerAsync(
        Guid trainerId,
        CancellationToken cancellationToken = default)
    {
        // The index decides, and here it also answers the list: the trainings a visitor may see
        // are exactly this person's entries, so "not offering" and "nothing to list" are one fact,
        // read once. The order is the catalog's own — the profile is a shelf of the same catalog,
        // not a second one (ADR 0001, ADR 0029).
        var offered = await trainingContext.Set<TrainingSearchEntry>()
            .AsNoTracking()
            .Where(entry => entry.TrainerId == trainerId
                && entry.IsPublished
                && !entry.IsTrainerHidden)
            .AlphabeticallyByTitle()
            .Select(entry => new CatalogTrainingDto
            {
                Id = entry.TrainingId,
                TrainerId = entry.TrainerId,
                Title = entry.Title
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (offered.Count == 0)
        {
            return null;
        }

        // The write model says who the person is, read now rather than indexed, for the detail's
        // reason: no fact carries a rename. The photo travels only with its stamp, the condition
        // the portrait itself is served under — a page offering an address the endpoint will
        // answer 404 renders a broken image rather than no image (ADR 0063).
        var id = TrainerId.Create(trainerId);

        var identity = await trainingContext.Trainers
            .AsNoTracking()
            .Where(trainer => trainer.Id == id)
            .Select(trainer => new
            {
                trainer.Name.Firstname,
                trainer.Name.Lastname,
                Bio = trainer.Bio!.Value,
                PhotoId = trainer.Photo!.SanitizedOnUtc != null ? trainer.Photo!.PhotoId : null
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // The index said yes and the row is gone: the entries are a moment stale, which is what an
        // eventually consistent read model is. Answering nothing is the same answer a visitor
        // would have got a second later.
        if (identity is null)
        {
            return null;
        }

        return new CatalogTrainerDto
        {
            Id = trainerId,
            Firstname = identity.Firstname,
            Lastname = identity.Lastname,
            Bio = identity.Bio,
            PhotoId = identity.PhotoId?.Value,
            Trainings = offered
        };
    }

    /// <inheritdoc />
    public async Task<TrainerPhotoDto?> FindTrainerPortraitAsync(
        Guid trainerId,
        Guid photoId,
        CancellationToken cancellationToken = default)
    {
        // The same first statement as the profile, and for the same reason: whether a person may
        // be looked at is composed in the index and nowhere here. Their portrait is content of the
        // profile page, so it inherits the profile's visibility rather than acquiring one of its
        // own.
        var isOffering = await trainingContext.Set<TrainingSearchEntry>()
            .AsNoTracking()
            .AnyAsync(
                entry => entry.TrainerId == trainerId
                    && entry.IsPublished
                    && !entry.IsTrainerHidden,
                cancellationToken)
            .ConfigureAwait(false);

        if (!isOffering)
        {
            return null;
        }

        // The same two conditions the training-addressed portrait states, minus the owner lookup
        // the route has already done: the identity must be the one this person has now, and the
        // stamp must be there (ADR 0063).
        var owner = TrainerId.Create(trainerId);
        var photo = PhotoId.Create(photoId);

        var portrait = await trainingContext.Trainers
            .AsNoTracking()
            .Where(trainer => trainer.Id == owner
                && trainer.Photo!.PhotoId == photo
                && trainer.Photo.SanitizedOnUtc != null)
            .Select(trainer => new { trainer.Photo!.ContentType })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (portrait is null)
        {
            return null;
        }

        var stored = await photoStore.FetchAsync(owner, photo, cancellationToken).ConfigureAwait(false);

        return stored is null
            ? null
            : new TrainerPhotoDto(photoId, stored.Content, portrait.ContentType);
    }
}
