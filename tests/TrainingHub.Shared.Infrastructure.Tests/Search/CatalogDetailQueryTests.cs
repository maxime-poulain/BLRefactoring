using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Tests.Helpers;
using TrainingHub.Shared.Infrastructure.Search;
using TrainingHub.Shared.Infrastructure.Storage;
using TrainingHub.Shared.Infrastructure.Tests.Queries;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;
using TrainingHub.Shared.Storage;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Search;

/// <summary>
/// What a visitor gets when they follow a search result (ADR 0062).
/// </summary>
/// <remarks>
/// Against SQLite through the real model, because both halves of this adapter are claims about SQL:
/// the index's entry decides visibility, and the write model's columns — including a name reached
/// through a correlated subquery — have to translate. The in-memory provider would answer both in
/// LINQ to objects and prove neither.
/// <para>
/// The entries are written by hand rather than by the indexer: what is under test is the reader, and
/// driving nine consumers to arrange one row would test them instead.
/// </para>
/// </remarks>
public sealed class CatalogDetailQueryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly InMemoryObjectStore _objects = new();
    private TrainingContext _context = null!;
    private ICatalogDetailQuery _detail = null!;
    private ITrainerPhotoStore _photos = null!;

    /// <summary>Opens the connection the database lives inside, and builds the schema on it.</summary>
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        _context = new TrainingContext(new DbContextOptionsBuilder<TrainingContext>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCustomizer, RowVersionWrittenByTheTest>()
            .Options);

        await _context.Database.EnsureCreatedAsync();

        // The real photo store over a fake object store: the key layout is part of what a portrait
        // read has to get right, and a stubbed ITrainerPhotoStore would agree with the reader by
        // construction.
        _photos = new TrainerPhotoStore(_objects);
        _detail = new CatalogDetailQuery(_context, _photos);
    }

    /// <summary>Closes the context and, with the connection, the database itself.</summary>
    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Find offered async, an offered training, answers it with its trainer's name.
    /// </summary>
    /// <remarks>
    /// The name is the column that makes this endpoint worth having, and it is the one the search
    /// index cannot hold — so a translation failure here would be the whole feature, silently.
    /// </remarks>
    [Fact]
    public async Task FindOfferedAsync_AnOfferedTraining_AnswersItWithItsTrainersName()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        var offered = await _detail.FindOfferedAsync(training.Id.Value);

        offered.Should().NotBeNull();
        offered!.Id.Should().Be(training.Id.Value);
        offered.Title.Should().Be("Domain Driven Design");
        offered.TrainerName.Should().Be("Ada Lovelace");
        offered.Description.Should().NotBeNullOrWhiteSpace();
        offered.Topics.Should().NotBeEmpty();
    }

    /// <summary>
    /// Find offered async, the trainer's name as it is now, rather than as it was.
    /// </summary>
    /// <remarks>
    /// The reason the name is read here rather than stored in the index: no integration event
    /// carries a rename, so an indexed copy would show the old name until something unrelated
    /// happened to the training. A live read cannot go stale, and this is what says so.
    /// </remarks>
    [Fact]
    public async Task FindOfferedAsync_AfterARename_AnswersTheNameAsItIsNow()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        trainer.Edit(
            Name.Create("Ada", "Byron").ShouldBeSuccess(),
            trainer.ContactEmail,
            trainer.Bio);
        await _context.SaveChangesAsync();

        var offered = await _detail.FindOfferedAsync(training.Id.Value);

        offered!.TrainerName.Should().Be("Ada Byron",
            "the name is read at the moment of the request, and nothing has to refresh a copy");
    }

    /// <summary>
    /// Find offered async, a training the index does not hold, answers nothing.
    /// </summary>
    /// <remarks>
    /// The whole visibility contract in one fact. The training exists and reads perfectly well from
    /// the write model; the only thing keeping it from a visitor is the absent entry, which is
    /// exactly where "on offer" is composed (ADR 0056).
    /// </remarks>
    [Fact]
    public async Task FindOfferedAsync_ATrainingTheIndexDoesNotHold_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");

        var offered = await _detail.FindOfferedAsync(training.Id.Value);

        offered.Should().BeNull();
    }

    /// <summary>
    /// Find offered async, an entry that is not published, answers nothing.
    /// </summary>
    [Fact]
    public async Task FindOfferedAsync_AnEntryThatIsNotPublished_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: false, isTrainerHidden: false);

        var offered = await _detail.FindOfferedAsync(training.Id.Value);

        offered.Should().BeNull();
    }

    /// <summary>
    /// Find offered async, an entry whose trainer is hidden, answers nothing.
    /// </summary>
    /// <remarks>
    /// The sanction's half of the composition. A training that is perfectly published disappears
    /// because its owner is suspended, and the reader never learns why — it asks the entry, not the
    /// trainer.
    /// </remarks>
    [Fact]
    public async Task FindOfferedAsync_AnEntryWhoseTrainerIsHidden_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: true);

        var offered = await _detail.FindOfferedAsync(training.Id.Value);

        offered.Should().BeNull();
    }

    /// <summary>
    /// Find offered async, an identifier nobody stored, answers nothing.
    /// </summary>
    [Fact]
    public async Task FindOfferedAsync_AnIdentifierNobodyStored_AnswersNothing()
    {
        var offered = await _detail.FindOfferedAsync(Guid.CreateVersion7());

        offered.Should().BeNull();
    }

    /// <summary>
    /// Find offered async, an owner with a stripped photo, answers the address of their portrait.
    /// </summary>
    /// <remarks>
    /// The detail and the portrait have to agree, and this is the half that says so from the
    /// detail's side: what the page renders is an address the endpoint below will answer, not a
    /// broken image.
    /// </remarks>
    [Fact]
    public async Task FindOfferedAsync_AnOwnerWithAStrippedPhoto_AnswersItsIdentity()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var photo = await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        var offered = await _detail.FindOfferedAsync(training.Id.Value);

        offered!.TrainerPhotoId.Should().Be(photo.PhotoId.Value);
    }

    /// <summary>
    /// Find offered async, an owner whose photo carries no stamp, answers no portrait at all.
    /// </summary>
    /// <remarks>
    /// The same refusal as the portrait endpoint's, one layer earlier. Publishing the address and
    /// then answering 404 on it would render a broken image, which is a worse answer than none.
    /// </remarks>
    [Fact]
    public async Task FindOfferedAsync_AnOwnerWhosePhotoCarriesNoStamp_AnswersNoPortrait()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Trainer SET PhotoSanitizedOnUtc = NULL WHERE Id = {0}", trainer.Id.Value);

        var offered = await _detail.FindOfferedAsync(training.Id.Value);

        offered!.TrainerPhotoId.Should().BeNull();
    }

    /// <summary>
    /// Find offered async, an owner with no photo, answers no portrait rather than throwing.
    /// </summary>
    /// <remarks>
    /// The three photo columns are null together and the identifier carries a value converter, so a
    /// projection that reached the column unconditionally would throw rather than answer nothing.
    /// </remarks>
    [Fact]
    public async Task FindOfferedAsync_AnOwnerWithNoPhoto_AnswersNoPortrait()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        var offered = await _detail.FindOfferedAsync(training.Id.Value);

        offered!.TrainerPhotoId.Should().BeNull();
    }

    /// <summary>
    /// Find offered portrait async, an offered training whose owner has a stripped photo, answers
    /// the bytes.
    /// </summary>
    /// <remarks>
    /// The happy path, and it is worth spelling out what it crosses: an index entry, a training row,
    /// a trainer row, a flattened value object with a converted identifier, and an object store
    /// keyed by two of those columns. Any one of them translating wrongly is a portrait nobody sees.
    /// </remarks>
    [Fact]
    public async Task FindOfferedPortraitAsync_AnOfferedTrainingWhoseOwnerHasAStrippedPhoto_AnswersTheBytes()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var photo = await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        var portrait = await _detail.FindOfferedPortraitAsync(training.Id.Value, photo.PhotoId.Value);

        portrait.Should().NotBeNull();
        portrait!.PhotoId.Should().Be(photo.PhotoId.Value);
        portrait.ContentType.Should().Be(TrainerPhoto.PngContentType);
        portrait.Content.ToArray().Should().Equal(Png());
    }

    /// <summary>
    /// Find offered portrait async, a photo stored before anything stripped it, answers nothing.
    /// </summary>
    /// <remarks>
    /// The precondition ADR 0062 named, from the only side that can produce it. The factory always
    /// stamps, so a null in that column can come from nothing but a row written before ADR 0063 —
    /// which is why this fact reaches past the domain and writes the null itself. The trainer's own
    /// profile still serves this portrait; what is refused is publishing it to a visitor.
    /// </remarks>
    [Fact]
    public async Task FindOfferedPortraitAsync_APhotoStoredBeforeAnythingStrippedIt_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var photo = await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Trainer SET PhotoSanitizedOnUtc = NULL WHERE Id = {0}", trainer.Id.Value);

        var portrait = await _detail.FindOfferedPortraitAsync(training.Id.Value, photo.PhotoId.Value);

        portrait.Should().BeNull(
            "what nothing can prove was stripped is not published, and the bytes are still there");
    }

    /// <summary>
    /// Find offered portrait async, a photo identity that is not the owner's current one, answers
    /// nothing.
    /// </summary>
    /// <remarks>
    /// What makes <c>immutable</c> honest, stated as a refusal. The address carries the identity, so
    /// a replaced portrait's address stops resolving rather than quietly serving the new picture
    /// under the old tag.
    /// </remarks>
    [Fact]
    public async Task FindOfferedPortraitAsync_APhotoTheOwnerDoesNotHave_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        var portrait = await _detail.FindOfferedPortraitAsync(training.Id.Value, Guid.CreateVersion7());

        portrait.Should().BeNull();
    }

    /// <summary>
    /// Find offered portrait async, a training the index does not hold, answers nothing.
    /// </summary>
    /// <remarks>
    /// The portrait inherits the training's visibility rather than acquiring one of its own: the
    /// photo is perfectly readable and its owner is in good standing, and the missing entry is the
    /// whole of the refusal.
    /// </remarks>
    [Fact]
    public async Task FindOfferedPortraitAsync_ATrainingTheIndexDoesNotHold_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var photo = await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");

        var portrait = await _detail.FindOfferedPortraitAsync(training.Id.Value, photo.PhotoId.Value);

        portrait.Should().BeNull();
    }

    /// <summary>
    /// Find offered portrait async, an entry whose trainer is hidden, answers nothing.
    /// </summary>
    [Fact]
    public async Task FindOfferedPortraitAsync_AnEntryWhoseTrainerIsHidden_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var photo = await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: true);

        var portrait = await _detail.FindOfferedPortraitAsync(training.Id.Value, photo.PhotoId.Value);

        portrait.Should().BeNull();
    }

    /// <summary>
    /// Find offered portrait async, an owner with no photo at all, answers nothing.
    /// </summary>
    /// <remarks>
    /// The three photo columns are null together, and the predicate has to survive that rather than
    /// throw converting a null identifier.
    /// </remarks>
    [Fact]
    public async Task FindOfferedPortraitAsync_AnOwnerWithNoPhoto_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        var portrait = await _detail.FindOfferedPortraitAsync(training.Id.Value, Guid.CreateVersion7());

        portrait.Should().BeNull();
    }

    /// <summary>
    /// Find offered portrait async, a row naming bytes the store lost, answers nothing.
    /// </summary>
    [Fact]
    public async Task FindOfferedPortraitAsync_BytesMissingFromTheStore_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var photo = await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        await _photos.DeleteAsync(trainer.Id, photo);

        var portrait = await _detail.FindOfferedPortraitAsync(training.Id.Value, photo.PhotoId.Value);

        portrait.Should().BeNull("a store is a separate system, and an error nobody can act on is worse");
    }

    /// <summary>
    /// Find offered async, an offered training, carries its trainer's identifier.
    /// </summary>
    /// <remarks>
    /// The identifier is what turns "Offered by X" into a link: the page builds the profile's
    /// address from it (ADR 0070), so a wrong or missing value is a link to nobody.
    /// </remarks>
    [Fact]
    public async Task FindOfferedAsync_AnOfferedTraining_CarriesItsTrainersIdentifier()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        var offered = await _detail.FindOfferedAsync(training.Id.Value);

        offered!.TrainerId.Should().Be(trainer.Id.Value);
    }

    /// <summary>
    /// Find offered trainer async, an offering trainer, answers who they are and what they offer.
    /// </summary>
    /// <remarks>
    /// The profile in one fact: identity from the write model, list from the index, and the two
    /// arrive together or not at all (ADR 0070).
    /// </remarks>
    [Fact]
    public async Task FindOfferedTrainerAsync_AnOfferingTrainer_AnswersWhoTheyAreAndWhatTheyOffer()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        var profile = await _detail.FindOfferedTrainerAsync(trainer.Id.Value);

        profile.Should().NotBeNull();
        profile!.Id.Should().Be(trainer.Id.Value);
        profile.Firstname.Should().Be("Ada");
        profile.Lastname.Should().Be("Lovelace");
        profile.Bio.Should().NotBeNullOrWhiteSpace();
        profile.Trainings.Should().ContainSingle(offered =>
            offered.Id == training.Id.Value
            && offered.TrainerId == trainer.Id.Value
            && offered.Title == "Domain Driven Design");
    }

    /// <summary>
    /// Find offered trainer async, the offered list, is alphabetical and holds only what is on
    /// offer.
    /// </summary>
    /// <remarks>
    /// The profile is a shelf of the same catalog: the order is the search's own (ADR 0001,
    /// ADR 0029), and an unpublished training is as absent here as it is there.
    /// </remarks>
    [Fact]
    public async Task FindOfferedTrainerAsync_TheOfferedList_IsAlphabeticalAndOnlyWhatIsOnOffer()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var zebra = await GivenTrainingAsync(trainer, "Zebra Patterns");
        var agile = await GivenTrainingAsync(trainer, "Agile Architecture");
        var draft = await GivenTrainingAsync(trainer, "Drafts Stay Home");
        await GivenIndexEntryAsync(zebra, trainer, isPublished: true, isTrainerHidden: false);
        await GivenIndexEntryAsync(agile, trainer, isPublished: true, isTrainerHidden: false);
        await GivenIndexEntryAsync(draft, trainer, isPublished: false, isTrainerHidden: false);

        var profile = await _detail.FindOfferedTrainerAsync(trainer.Id.Value);

        profile!.Trainings.Select(offered => offered.Title)
            .Should().Equal("Agile Architecture", "Zebra Patterns");
    }

    /// <summary>
    /// Find offered trainer async, a trainer the index hides, answers nothing.
    /// </summary>
    /// <remarks>
    /// The suspension's read, without ever reading the suspension: the entries are there, the flag
    /// is up, and the profile is as gone as the search results already are (ADR 0070).
    /// </remarks>
    [Fact]
    public async Task FindOfferedTrainerAsync_ATrainerTheIndexHides_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: true);

        var profile = await _detail.FindOfferedTrainerAsync(trainer.Id.Value);

        profile.Should().BeNull();
    }

    /// <summary>
    /// Find offered trainer async, a trainer with nothing published, answers nothing.
    /// </summary>
    /// <remarks>
    /// Offered or invisible, with no state in between: a registered person with only drafts has no
    /// public page, and their 404 is indistinguishable from anybody else's (ADR 0070).
    /// </remarks>
    [Fact]
    public async Task FindOfferedTrainerAsync_ATrainerWithNothingPublished_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: false, isTrainerHidden: false);

        var profile = await _detail.FindOfferedTrainerAsync(trainer.Id.Value);

        profile.Should().BeNull();
    }

    /// <summary>
    /// Find offered trainer async, an identifier nobody stored, answers nothing.
    /// </summary>
    [Fact]
    public async Task FindOfferedTrainerAsync_AnIdentifierNobodyStored_AnswersNothing()
    {
        var profile = await _detail.FindOfferedTrainerAsync(Guid.CreateVersion7());

        profile.Should().BeNull();
    }

    /// <summary>
    /// Find offered trainer async, a trainer who says nothing about themselves, answers a null bio.
    /// </summary>
    /// <remarks>
    /// The bio column is nullable inside a complex property, and a projection that reached it
    /// carelessly would either throw or answer an empty string a page would render as a blank
    /// paragraph.
    /// </remarks>
    [Fact]
    public async Task FindOfferedTrainerAsync_ATrainerWhoSaysNothing_AnswersANullBio()
    {
        var trainer = new TrainerBuilder()
            .WithFirstname("Ada")
            .WithLastname("Lovelace")
            .WithContactEmail($"trainer.{Guid.NewGuid():N}@example.com")
            .WithoutBio()
            .Build();
        _context.Trainers.Add(trainer);
        await _context.SaveChangesAsync();
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        var profile = await _detail.FindOfferedTrainerAsync(trainer.Id.Value);

        profile!.Bio.Should().BeNull();
    }

    /// <summary>
    /// Find offered trainer async, a stripped photo, answers the address of the portrait.
    /// </summary>
    [Fact]
    public async Task FindOfferedTrainerAsync_AStrippedPhoto_AnswersItsIdentity()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var photo = await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        var profile = await _detail.FindOfferedTrainerAsync(trainer.Id.Value);

        profile!.PhotoId.Should().Be(photo.PhotoId.Value);
    }

    /// <summary>
    /// Find offered trainer async, a photo carrying no stamp, answers no portrait at all.
    /// </summary>
    /// <remarks>
    /// The same refusal as the detail's, on the profile's page: an address the endpoint will
    /// answer 404 renders a broken image, which is a worse answer than none (ADR 0063).
    /// </remarks>
    [Fact]
    public async Task FindOfferedTrainerAsync_APhotoCarryingNoStamp_AnswersNoPortrait()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Trainer SET PhotoSanitizedOnUtc = NULL WHERE Id = {0}", trainer.Id.Value);

        var profile = await _detail.FindOfferedTrainerAsync(trainer.Id.Value);

        profile!.PhotoId.Should().BeNull();
    }

    /// <summary>
    /// Find trainer portrait async, an offering trainer with a stripped photo, answers the bytes.
    /// </summary>
    /// <remarks>
    /// The profile's own address for the portrait, crossing the same layers as the
    /// training-addressed one minus the owner lookup the route already did (ADR 0070).
    /// </remarks>
    [Fact]
    public async Task FindTrainerPortraitAsync_AnOfferingTrainerWithAStrippedPhoto_AnswersTheBytes()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var photo = await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        var portrait = await _detail.FindTrainerPortraitAsync(trainer.Id.Value, photo.PhotoId.Value);

        portrait.Should().NotBeNull();
        portrait!.PhotoId.Should().Be(photo.PhotoId.Value);
        portrait.ContentType.Should().Be(TrainerPhoto.PngContentType);
        portrait.Content.ToArray().Should().Equal(Png());
    }

    /// <summary>
    /// Find trainer portrait async, a trainer the index does not hold, answers nothing.
    /// </summary>
    /// <remarks>
    /// The portrait inherits the profile's visibility rather than acquiring one of its own: the
    /// photo is perfectly readable, and the missing entry is the whole of the refusal (ADR 0070).
    /// </remarks>
    [Fact]
    public async Task FindTrainerPortraitAsync_ATrainerTheIndexDoesNotHold_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var photo = await GivenPortraitAsync(trainer);

        var portrait = await _detail.FindTrainerPortraitAsync(trainer.Id.Value, photo.PhotoId.Value);

        portrait.Should().BeNull();
    }

    /// <summary>
    /// Find trainer portrait async, a photo identity that is not the current one, answers nothing.
    /// </summary>
    [Fact]
    public async Task FindTrainerPortraitAsync_APhotoTheTrainerDoesNotHave_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        var portrait = await _detail.FindTrainerPortraitAsync(trainer.Id.Value, Guid.CreateVersion7());

        portrait.Should().BeNull();
    }

    /// <summary>
    /// Find trainer portrait async, a photo stored before anything stripped it, answers nothing.
    /// </summary>
    [Fact]
    public async Task FindTrainerPortraitAsync_APhotoStoredBeforeAnythingStrippedIt_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var photo = await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Trainer SET PhotoSanitizedOnUtc = NULL WHERE Id = {0}", trainer.Id.Value);

        var portrait = await _detail.FindTrainerPortraitAsync(trainer.Id.Value, photo.PhotoId.Value);

        portrait.Should().BeNull(
            "what nothing can prove was stripped is not published, and the bytes are still there");
    }

    /// <summary>
    /// Find trainer portrait async, a row naming bytes the store lost, answers nothing.
    /// </summary>
    [Fact]
    public async Task FindTrainerPortraitAsync_BytesMissingFromTheStore_AnswersNothing()
    {
        var trainer = await GivenTrainerAsync("Ada", "Lovelace");
        var photo = await GivenPortraitAsync(trainer);
        var training = await GivenTrainingAsync(trainer, "Domain Driven Design");
        await GivenIndexEntryAsync(training, trainer, isPublished: true, isTrainerHidden: false);

        await _photos.DeleteAsync(trainer.Id, photo);

        var portrait = await _detail.FindTrainerPortraitAsync(trainer.Id.Value, photo.PhotoId.Value);

        portrait.Should().BeNull("a store is a separate system, and an error nobody can act on is worse");
    }

    private async Task<TrainerPhoto> GivenPortraitAsync(Trainer trainer)
    {
        var photo = TrainerPhoto
            .Create(Png(), TrainerPhoto.PngContentType, new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc))
            .ShouldBeSuccess();

        trainer.AttachPhoto(photo);

        await _context.SaveChangesAsync();
        await _photos.StoreAsync(trainer.Id, photo, Png());

        return photo;
    }

    /// <summary>The eight bytes that make a PNG a PNG, which is all the factory reads.</summary>
    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02];

    /// <summary>
    /// An object store that keeps its bytes in a dictionary.
    /// </summary>
    /// <remarks>
    /// Small enough to be here rather than shared: what these facts need from a store is that it
    /// answers what was put under a key and nothing under any other. The S3 adapter's own behavior
    /// is somebody else's test, and needs SeaweedFS to be honest about.
    /// </remarks>
    private sealed class InMemoryObjectStore : IObjectStore
    {
        private readonly Dictionary<string, StoredObject> _objects = [];

        public Task PutAsync(
            ObjectKey key,
            ReadOnlyMemory<byte> content,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            _objects[key.Value] = new StoredObject(content, contentType);

            return Task.CompletedTask;
        }

        public Task<StoredObject?> GetAsync(ObjectKey key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_objects.GetValueOrDefault(key.Value));

        public Task DeleteAsync(ObjectKey key, CancellationToken cancellationToken = default)
        {
            _objects.Remove(key.Value);

            return Task.CompletedTask;
        }
    }

    private async Task<Trainer> GivenTrainerAsync(string firstname, string lastname)
    {
        var trainer = new TrainerBuilder()
            .WithFirstname(firstname)
            .WithLastname(lastname)
            .WithContactEmail($"trainer.{Guid.NewGuid():N}@example.com")
            .Build();

        _context.Trainers.Add(trainer);
        await _context.SaveChangesAsync();

        return trainer;
    }

    private async Task<Training> GivenTrainingAsync(Trainer owner, string title)
    {
        var training = (await new TrainingBuilder()
            .WithTitle(title)
            .WithTrainerId(owner.Id.Value)
            .BuildAsync()).ShouldBeSuccess();

        _context.Trainings.Add(training);
        await _context.SaveChangesAsync();

        return training;
    }

    private async Task GivenIndexEntryAsync(
        Training training,
        Trainer owner,
        bool isPublished,
        bool isTrainerHidden)
    {
        var entry = new TrainingSearchEntry(training.Id.Value);
        entry.Describe(
            owner.Id.Value, training.Title.Value, training.CreatedOn, isPublished, isTrainerHidden, [], []);

        _context.Set<TrainingSearchEntry>().Add(entry);
        await _context.SaveChangesAsync();
    }
}
