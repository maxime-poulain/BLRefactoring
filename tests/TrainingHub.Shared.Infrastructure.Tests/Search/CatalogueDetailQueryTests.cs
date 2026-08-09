using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TrainingHub.Shared.Application.Catalogue;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Tests.Helpers;
using TrainingHub.Shared.Infrastructure.Search;
using TrainingHub.Shared.Infrastructure.Tests.Queries;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;
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
public sealed class CatalogueDetailQueryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private TrainingContext _context = null!;
    private ICatalogueDetailQuery _detail = null!;

    /// <summary>Opens the connection the database lives inside, and builds the schema on it.</summary>
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        _context = new TrainingContext(new DbContextOptionsBuilder<TrainingContext>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCustomizer, RowVersionWrittenByTheTest>()
            .Options);

        await _context.Database.EnsureCreatedAsync();

        _detail = new CatalogueDetailQuery(_context);
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
        entry.Describe(owner.Id.Value, training.Title.Value, isPublished, isTrainerHidden, []);

        _context.Set<TrainingSearchEntry>().Add(entry);
        await _context.SaveChangesAsync();
    }
}
