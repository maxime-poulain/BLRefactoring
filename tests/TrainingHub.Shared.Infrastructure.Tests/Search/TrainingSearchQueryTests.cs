using AwesomeAssertions;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Infrastructure.Search;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Search;

/// <summary>
/// The one question the search index answers, asked against a real index.
/// </summary>
/// <remarks>
/// The index is filled through the adapter rather than by hand, so these facts are about the pair:
/// a reader that agreed with a hand-written row and disagreed with the writer would pass here and
/// serve nothing in production.
/// </remarks>
public sealed class TrainingSearchQueryTests : SearchIndexTest
{
    /// <summary>
    /// Search async, two words, answers only what matches both.
    /// </summary>
    /// <remarks>
    /// The property the composed <c>EXISTS</c> exists for. A reader that joined the tokens with
    /// <c>OR</c> would widen as the caller typed, which is a search that gets worse the more it is
    /// told.
    /// </remarks>
    [Fact]
    public async Task SearchAsync_TwoWords_AnswersOnlyWhatMatchesBoth()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Domain Driven Design");
        await IndexedAsync(trainer, "Domain Modeling");
        await IndexedAsync(trainer, "Driven To Distraction");

        var page = await Query().SearchAsync("domain driven", new PageRequest());

        page.Items.Should().ContainSingle().Which.Title.Should().Be("Domain Driven Design");
        page.TotalCount.Should().Be(1);
    }

    /// <summary>
    /// Search async, a prefix, answers the words that start with it.
    /// </summary>
    /// <remarks>
    /// Prefix rather than substring, and that is the whole difference from the <c>LIKE '%term%'</c>
    /// ADR 0055 recorded: this one can seek along the index. The negative half is asserted with it,
    /// because a reader that had quietly gone back to a substring match would pass the positive one.
    /// </remarks>
    [Fact]
    public async Task SearchAsync_APrefix_AnswersTheWordsThatStartWithIt()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Refactoring Legacy Code");
        await IndexedAsync(trainer, "Craftsmanship");

        var page = await Query().SearchAsync("refac", new PageRequest());

        page.Items.Should().ContainSingle().Which.Title.Should().Be("Refactoring Legacy Code");

        var inside = await Query().SearchAsync("actor", new PageRequest());

        inside.Items.Should().BeEmpty();
    }

    /// <summary>
    /// Search async, any casing, answers the same page.
    /// </summary>
    /// <remarks>
    /// Both sides normalize through the same method, so this holds without depending on the
    /// database's collation — which SQLite and SQL Server do not agree about.
    /// </remarks>
    [Theory]
    [InlineData("EVENT")]
    [InlineData("event")]
    [InlineData("EvEnT")]
    public async Task SearchAsync_AnyCasing_AnswersTheSamePage(string term)
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Event Storming");

        var page = await Query().SearchAsync(term, new PageRequest());

        page.Items.Should().ContainSingle().Which.Title.Should().Be("Event Storming");
    }

    /// <summary>
    /// Search async, a withdrawn training, does not offer it.
    /// </summary>
    /// <remarks>
    /// A creation is indexed before anything is withdrawn, so an entry that is not on offer is the
    /// ordinary case rather than the exotic one. What must never happen is that it answers.
    /// </remarks>
    [Fact]
    public async Task SearchAsync_AWithdrawnTraining_DoesNotOfferIt()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Kept Quiet", published: false);

        var page = await Query().SearchAsync("kept", new PageRequest());

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    /// <summary>
    /// Search async, after a sanction, offers the catalog again only once it is lifted.
    /// </summary>
    /// <remarks>
    /// The end of the chain ADR 0056 designed: one call about a trainer takes their whole catalog
    /// out of the public's reach, and one call puts it back. Asserted through the reader rather than
    /// on the column, because what the record promises is what a visitor sees.
    /// </remarks>
    [Fact]
    public async Task SearchAsync_AfterASanction_OffersTheCatalogAgainOnlyOnceItIsLifted()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Sanctioned Course");

        await Indexer.HideTrainerCatalogAsync(trainer.Id.Value);
        (await Query().SearchAsync("sanctioned", new PageRequest())).Items.Should().BeEmpty();

        await Indexer.ShowTrainerCatalogAsync(trainer.Id.Value);
        (await Query().SearchAsync("sanctioned", new PageRequest())).Items.Should().ContainSingle();
    }

    /// <summary>
    /// Search async, no term at all, answers the offered catalog in its total order.
    /// </summary>
    /// <remarks>
    /// A blank term is no term, the reading the trainers' listing already gives it (ADR 0055). The
    /// order is asserted because it is the property paging rests on: ADR 0001 wants a total one, and
    /// this index has no creation date to use, so it sorts on the title.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task SearchAsync_NoTermAtAll_AnswersTheOfferedCatalogInItsTotalOrder(string? term)
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Beta Course");
        await IndexedAsync(trainer, "Alpha Course");
        await IndexedAsync(trainer, "Hidden Course", published: false);

        var page = await Query().SearchAsync(term, new PageRequest());

        page.Items.Select(training => training.Title).Should().Equal("Alpha Course", "Beta Course");
    }

    /// <summary>
    /// Search async, a page smaller than the match, counts everything that matched.
    /// </summary>
    /// <remarks>
    /// The defect ADR 0055 rejects by name when it refuses to filter after paging: a count that
    /// described the page rather than the set would look right in a demo and be wrong in every
    /// number it printed.
    /// </remarks>
    [Fact]
    public async Task SearchAsync_APageSmallerThanTheMatch_CountsEverythingThatMatched()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Course Alpha");
        await IndexedAsync(trainer, "Course Beta");
        await IndexedAsync(trainer, "Course Gamma");

        var page = await Query().SearchAsync("course", new PageRequest { PageSize = 2 });

        page.Items.Should().HaveCount(2);
        page.TotalCount.Should().Be(3);
        page.HasNextPage.Should().BeTrue();
    }

    /// <summary>
    /// Search async, a term of punctuation alone, answers the whole catalog rather than nothing.
    /// </summary>
    /// <remarks>
    /// A term that yields no token is a term that narrows nothing, and it has to mean the same thing
    /// as no term at all — otherwise a caller typing a stray character would read an empty catalog
    /// as "there is nothing here".
    /// </remarks>
    [Fact]
    public async Task SearchAsync_ATermOfPunctuationAlone_AnswersTheWholeCatalogRatherThanNothing()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Anything At All");

        var page = await Query().SearchAsync("--- !", new PageRequest());

        page.Items.Should().ContainSingle();
    }

    private TrainingSearchQuery Query() => new(Context);

    private async Task IndexedAsync(Trainer owner, string title, bool published = true)
    {
        var training = await GivenTrainingAsync(owner, title, published);

        await Indexer.IndexAsync(training.Id.Value, owner.Id.Value);
    }
}
