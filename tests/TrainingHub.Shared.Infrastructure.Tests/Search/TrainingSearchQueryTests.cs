using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Infrastructure.Search;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Search;
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

        var page = await Query().SearchAsync("domain driven", [], CatalogOrder.Title, new PageRequest());

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

        var page = await Query().SearchAsync("refac", [], CatalogOrder.Title, new PageRequest());

        page.Items.Should().ContainSingle().Which.Title.Should().Be("Refactoring Legacy Code");

        var inside = await Query().SearchAsync("actor", [], CatalogOrder.Title, new PageRequest());

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

        var page = await Query().SearchAsync(term, [], CatalogOrder.Title, new PageRequest());

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

        var page = await Query().SearchAsync("kept", [], CatalogOrder.Title, new PageRequest());

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
        (await Query().SearchAsync("sanctioned", [], CatalogOrder.Title, new PageRequest())).Items.Should().BeEmpty();

        await Indexer.ShowTrainerCatalogAsync(trainer.Id.Value);
        (await Query().SearchAsync("sanctioned", [], CatalogOrder.Title, new PageRequest())).Items.Should().ContainSingle();
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

        var page = await Query().SearchAsync(term, [], CatalogOrder.Title, new PageRequest());

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

        var page = await Query().SearchAsync("course", [], CatalogOrder.Title, new PageRequest { PageSize = 2 });

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

        var page = await Query().SearchAsync("--- !", [], CatalogOrder.Title, new PageRequest());

        page.Items.Should().ContainSingle();
    }

    /// <summary>
    /// Search async, a topic, answers only that shelf.
    /// </summary>
    /// <remarks>
    /// Equality against the topics' own index rather than a prefix, because a topic is a name from
    /// a closed set, not a word somebody is still typing (ADR 0069).
    /// </remarks>
    [Fact]
    public async Task SearchAsync_ATopic_AnswersOnlyThatShelf()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Refactoring Legacy Code", topics: ["Programming"]);
        await IndexedAsync(trainer, "Design Sprints", topics: ["Design"]);

        var page = await Query().SearchAsync(null, ["Design"], CatalogOrder.Title, new PageRequest());

        page.Items.Should().ContainSingle().Which.Title.Should().Be("Design Sprints");
    }

    /// <summary>
    /// Search async, a topic and a term, narrows by both at once.
    /// </summary>
    [Fact]
    public async Task SearchAsync_ATopicAndATerm_NarrowsByBothAtOnce()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Advanced Modeling", topics: ["Programming"]);
        await IndexedAsync(trainer, "Advanced Sketching", topics: ["Design"]);
        await IndexedAsync(trainer, "Basic Sketching", topics: ["Design"]);

        var page = await Query().SearchAsync("advanced", ["Design"], CatalogOrder.Title, new PageRequest());

        page.Items.Should().ContainSingle().Which.Title.Should().Be("Advanced Sketching");
    }

    /// <summary>
    /// Search async, the newest order, answers the youngest training first.
    /// </summary>
    /// <remarks>
    /// The age is the training's own <c>CreatedOn</c>, read back from the write model (ADR 0071):
    /// the suite's clock moves a second per reading, so the third training saved is the youngest
    /// and must lead — where the default order would put "Alpha" first.
    /// </remarks>
    [Fact]
    public async Task SearchAsync_TheNewestOrder_AnswersTheYoungestTrainingFirst()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Zebra Patterns");
        await IndexedAsync(trainer, "Middle Management");
        await IndexedAsync(trainer, "Alpha Testing");

        var page = await Query().SearchAsync(null, [], CatalogOrder.Newest, new PageRequest());

        page.Items.Select(training => training.Title).Should().ContainInOrder(
            "Alpha Testing", "Middle Management", "Zebra Patterns");
    }

    /// <summary>
    /// Search async, the newest order with tied ages, breaks the tie by identifier.
    /// </summary>
    /// <remarks>
    /// The property ADR 0001 demands of every order: total, so a row cannot appear on two pages or
    /// on none. Ties are manufactured by flattening the column after indexing, because the reader —
    /// not the writer — is what this fact is about.
    /// </remarks>
    [Fact]
    public async Task SearchAsync_TheNewestOrderWithTiedAges_BreaksTheTieByIdentifier()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "First Saved");
        await IndexedAsync(trainer, "Second Saved");

        var sameInstant = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        await Context.Set<TrainingSearchEntry>()
            .ExecuteUpdateAsync(entry => entry.SetProperty(row => row.CreatedOnUtc, sameInstant));

        var page = await Query().SearchAsync(null, [], CatalogOrder.Newest, new PageRequest());

        page.Items.Select(training => training.Id)
            .Should().BeInAscendingOrder("the identifier settles what the clock left tied");
    }

    /// <summary>
    /// Search async, the newest order, composes with the term, the topic and the page.
    /// </summary>
    /// <remarks>
    /// The order is one axis of one composed query, not a different question: the filters narrow
    /// the same set they narrow under the default order, and the count still describes what the
    /// page walks (ADR 0029, ADR 0071).
    /// </remarks>
    [Fact]
    public async Task SearchAsync_TheNewestOrder_ComposesWithTheTermTheTopicAndThePage()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Course One", topics: ["Design"]);
        await IndexedAsync(trainer, "Course Two", topics: ["Design"]);
        await IndexedAsync(trainer, "Course Three", topics: ["Marketing"]);
        await IndexedAsync(trainer, "Course Four", topics: ["Design"]);

        var page = await Query().SearchAsync(
            "course", ["Design"], CatalogOrder.Newest, new PageRequest { PageSize = 2 });

        page.TotalCount.Should().Be(3, "the topic filter narrows the count the page describes");
        page.Items.Select(training => training.Title).Should().ContainInOrder(
            "Course Four", "Course Two");
    }

    /// <summary>
    /// Facets async, counts only what is offered.
    /// </summary>
    /// <remarks>
    /// The same composed visibility the search reads (ADR 0050, ADR 0056): a withdrawn training
    /// and a sanctioned trainer's whole shelf leave these numbers the moment their consumers run,
    /// so a facet never promises a shelf the search would answer empty (ADR 0069).
    /// </remarks>
    [Fact]
    public async Task FacetsAsync_CountsOnlyWhatIsOffered()
    {
        var trainer = await GivenTrainerAsync();
        var sanctioned = await GivenTrainerAsync(suspended: true);

        await IndexedAsync(trainer, "On Offer", topics: ["Programming"]);
        await IndexedAsync(trainer, "Withdrawn", published: false, topics: ["Programming"]);
        await IndexedAsync(sanctioned, "Hidden Whole", topics: ["Programming"]);

        var facets = await Query().FacetsAsync(null);

        facets.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Topic = "Programming", OfferedCount = 1 });
    }

    /// <summary>
    /// Search async, two shelves, answers their union rather than their intersection.
    /// </summary>
    /// <remarks>
    /// <b>The fact ADR 0080 exists for.</b> A ticked shelf widens the answer, so a training filed
    /// under either one appears and a training under neither does not. Written with three trainings
    /// on purpose: the one under both is what tells a union from an intersection — under an
    /// intersection it would be the only answer, and the count would be one rather than three.
    /// </remarks>
    [Fact]
    public async Task SearchAsync_TwoShelves_AnswersTheirUnionRatherThanTheirIntersection()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Only Design", topics: ["Design"]);
        await IndexedAsync(trainer, "Only Programming", topics: ["Programming"]);
        await IndexedAsync(trainer, "Both Of Them", topics: ["Design", "Programming"]);
        await IndexedAsync(trainer, "Neither Of Them", topics: ["Marketing"]);

        var page = await Query().SearchAsync(
            null, ["Design", "Programming"], CatalogOrder.Title, new PageRequest());

        page.TotalCount.Should().Be(3, "a shelf a visitor ticks widens the answer rather than narrowing it");
        page.Items.Select(training => training.Title).Should().BeEquivalentTo(
            "Only Design", "Only Programming", "Both Of Them");
    }

    /// <summary>
    /// Search async, a training on both shelves, is answered once.
    /// </summary>
    /// <remarks>
    /// The union is over trainings, not over rows: the filter is one <c>EXISTS</c> rather than a
    /// join, so a training carrying both names cannot come back twice. A page that repeated it
    /// would also make the count disagree with the rows, which is the defect ADR 0055 names.
    /// </remarks>
    [Fact]
    public async Task SearchAsync_ATrainingOnBothShelves_IsAnsweredOnce()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Both Of Them", topics: ["Design", "Programming"]);

        var page = await Query().SearchAsync(
            null, ["Design", "Programming"], CatalogOrder.Title, new PageRequest());

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle();
    }

    /// <summary>
    /// Search async, no shelf at all, answers the whole catalog.
    /// </summary>
    /// <remarks>
    /// Empty is every topic rather than none — a visitor who has ticked nothing is asking for the
    /// whole catalog, not for an empty one. The reading a collection makes easy to get backwards.
    /// </remarks>
    [Fact]
    public async Task SearchAsync_NoShelfAtAll_AnswersTheWholeCatalog()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Only Design", topics: ["Design"]);
        await IndexedAsync(trainer, "Only Marketing", topics: ["Marketing"]);

        (await Query().SearchAsync(null, [], CatalogOrder.Title, new PageRequest()))
            .TotalCount.Should().Be(2);
    }

    /// <summary>
    /// Search async, two shelves and a term, narrows by the term across both.
    /// </summary>
    /// <remarks>
    /// The two filters compose in opposite directions and this is where that shows: the shelves
    /// widen, the term narrows, and what comes back is what any shelf carries <em>and</em> the term
    /// matches.
    /// </remarks>
    [Fact]
    public async Task SearchAsync_TwoShelvesAndATerm_NarrowsByTheTermAcrossBoth()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Advanced Design", topics: ["Design"]);
        await IndexedAsync(trainer, "Advanced Programming", topics: ["Programming"]);
        await IndexedAsync(trainer, "Beginner Design", topics: ["Design"]);

        var page = await Query().SearchAsync(
            "advanced", ["Design", "Programming"], CatalogOrder.Title, new PageRequest());

        page.Items.Select(training => training.Title).Should().Equal(
            "Advanced Design", "Advanced Programming");
    }

    /// <summary>
    /// Facets async, a term, counts only what that term leaves standing.
    /// </summary>
    /// <remarks>
    /// <b>The other half of ADR 0080.</b> A chip advertising twelve trainings when the visitor's
    /// term reaches two of them is a number that answers a question nobody asked. Counted under the
    /// term, each facet says what it would contribute to the search as typed.
    /// </remarks>
    [Fact]
    public async Task FacetsAsync_ATerm_CountsOnlyWhatThatTermLeavesStanding()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Advanced Design", topics: ["Design"]);
        await IndexedAsync(trainer, "Beginner Design", topics: ["Design"]);
        await IndexedAsync(trainer, "Advanced Marketing", topics: ["Marketing"]);

        var facets = await Query().FacetsAsync("advanced");

        facets.Should().BeEquivalentTo(new[]
        {
            new { Topic = "Design", OfferedCount = 1 },
            new { Topic = "Marketing", OfferedCount = 1 }
        });
    }

    /// <summary>
    /// Facets async, a shelf the term empties, is absent rather than zero.
    /// </summary>
    /// <remarks>
    /// The promise ADR 0069 made about what is on offer, kept for the term as well: a chip is a way
    /// in, and one leading nowhere should not be offered at all rather than offered at nought.
    /// </remarks>
    [Fact]
    public async Task FacetsAsync_AShelfTheTermEmpties_IsAbsentRatherThanZero()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Advanced Design", topics: ["Design"]);
        await IndexedAsync(trainer, "Beginner Marketing", topics: ["Marketing"]);

        var facets = await Query().FacetsAsync("advanced");

        facets.Should().ContainSingle()
            .Which.Topic.Should().Be("Design");
    }

    /// <summary>
    /// Facets async, a topic nothing offered declares, is absent rather than zero.
    /// </summary>
    [Fact]
    public async Task FacetsAsync_ATopicNothingOfferedDeclares_IsAbsentRatherThanZero()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Withdrawn Shelf", published: false, topics: ["Marketing"]);

        (await Query().FacetsAsync(null)).Should().BeEmpty(
            "a facet is a way into the catalog, and an empty shelf leads nowhere (ADR 0069)");
    }

    /// <summary>
    /// Facets async, several shelves, answers them alphabetically.
    /// </summary>
    [Fact]
    public async Task FacetsAsync_SeveralShelves_AnswersThemAlphabetically()
    {
        var trainer = await GivenTrainerAsync();

        await IndexedAsync(trainer, "Selling Well", topics: ["Marketing"]);
        await IndexedAsync(trainer, "Leading Kindly", topics: ["Leadership"]);
        await IndexedAsync(trainer, "Sketching Fast", topics: ["Design", "Marketing"]);

        var facets = await Query().FacetsAsync(null);

        facets.Select(facet => facet.Topic)
            .Should().ContainInOrder("Design", "Leadership", "Marketing");
        facets.Single(facet => facet.Topic == "Marketing").OfferedCount.Should().Be(2);
    }

    private TrainingSearchQuery Query() => new(Context);

    private async Task IndexedAsync(
        Trainer owner, string title, bool published = true, string[]? topics = null)
    {
        var training = await GivenTrainingAsync(owner, title, published, topics);

        await Indexer.IndexAsync(training.Id.Value, owner.Id.Value);
    }
}
