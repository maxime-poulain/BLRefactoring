using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Common.Pagination;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The public catalog over HTTP, on both hosts: what a visitor with no token can find, and what
/// the write side has to do before they can (ADR 0059).
/// </summary>
/// <remarks>
/// The only suite here that proves the whole chain of the Search Indexing context end to end: a
/// write commits its fact, the delivery worker replays it into the index after the commit
/// (ADR 0002, ADR 0025), and an anonymous read answers from the index rather than from the
/// trainings table. Everything in between is exercised elsewhere against substitutes; none of it
/// proves that a visitor eventually sees anything.
/// <para>
/// Every assertion polls, because the index is eventually consistent by design and that is the
/// property under test rather than a nuisance to work around. The polling shape is
/// <c>OutboxTest.WaitForMessageAsync</c>'s.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class CatalogSearchTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IServiceScopeSource, IHttpClientSource
{
    /// <summary>
    /// A published training, becomes findable by a word of its title, to a caller with no token.
    /// </summary>
    /// <remarks>
    /// Anonymous on purpose: every other read of this API is refused <c>401</c> without one, and the
    /// difference is the whole point of the fourth controller base. The term is one word of the
    /// title rather than the title itself, because matching a whole string would pass against an
    /// index that had never been tokenized.
    /// </remarks>
    [Fact]
    public async Task APublishedTraining_BecomesFindableByAWordOfItsTitle_ToACallerWithNoToken()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var created = await trainer.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("Hexagonal Architecture Explained"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var trainingId = await created.Content.ReadFromJsonAsync<Guid>();

        (await WaitForCatalogAsync("hexagonal", holds: trainingId)).Should().Contain(trainingId);
    }

    /// <summary>
    /// A withdrawn training, leaves the catalog.
    /// </summary>
    /// <remarks>
    /// The defect ADR 0050 named from the other end: without the removal, an index would go on
    /// serving a training its owner has taken back, and the state would be a lie the write side
    /// alone could not detect.
    /// </remarks>
    [Fact]
    public async Task AWithdrawnTraining_LeavesTheCatalog()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var created = await trainer.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("Withdrawn Before Long"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var trainingId = await created.Content.ReadFromJsonAsync<Guid>();

        (await WaitForCatalogAsync("withdrawn", holds: trainingId)).Should().Contain(trainingId);

        (await trainer.PostAsync($"/Training/{trainingId}/unpublish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await WaitForCatalogAsync("withdrawn", holdsNot: trainingId)).Should().NotContain(trainingId);
    }

    /// <summary>
    /// A suspended trainer's catalog, leaves public view, and comes back when the sanction is
    /// lifted.
    /// </summary>
    /// <remarks>
    /// The end of the chain ADR 0056 designed, asserted where it matters: not on a column, but on
    /// what somebody outside the product can see. The lifting is half the fact — an index that had
    /// deleted the entries would need the catalog read back to rebuild them, which is precisely
    /// the design that record rejected.
    /// </remarks>
    [Fact]
    public async Task ASuspendedTrainersCatalog_LeavesPublicView_AndComesBackWhenTheSanctionIsLifted()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainerId = (await trainer.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!.Id;

        var created = await trainer.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("Sanctioned Catalog Entry"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var trainingId = await created.Content.ReadFromJsonAsync<Guid>();

        (await WaitForCatalogAsync("sanctioned", holds: trainingId)).Should().Contain(trainingId);

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        (await administrator.PostAsJsonAsync(
                $"/Administration/trainers/{trainerId}/suspend",
                new SuspendTrainerHttpRequest { Reason = "Repeated breaches of the content policy." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await WaitForCatalogAsync("sanctioned", holdsNot: trainingId)).Should().NotContain(trainingId);

        (await administrator.PostAsync(
                $"/Administration/trainers/{trainerId}/reinstate", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await WaitForCatalogAsync("sanctioned", holds: trainingId)).Should().Contain(trainingId);
    }

    /// <summary>
    /// A term longer than a title, is refused rather than answered with an empty page.
    /// </summary>
    /// <remarks>
    /// An empty page is a legitimate answer to a good question, so it must not also be the answer to
    /// a bad one — the reading ADR 0055 gives an unknown status, applied to a term.
    /// </remarks>
    [Fact]
    public async Task ATermLongerThanATitle_IsRefusedRatherThanAnsweredWithAnEmptyPage()
    {
        var anonymous = Factory.CreateClient();

        var refused = await anonymous.GetAsync(
            $"/Catalog/trainings?term={new string('a', 101)}");

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The catalogs facets, follow what is offered, to a caller with no token.
    /// </summary>
    /// <remarks>
    /// The whole chain of ADR 0069 in one fact: publishing files the training under its topic
    /// through the outbox, and withdrawing takes the shelf away — absent rather than zero, because
    /// a facet is a way into the catalog and an empty shelf leads nowhere.
    /// </remarks>
    [Fact]
    public async Task TheCatalogsFacets_FollowWhatIsOffered_ToACallerWithNoToken()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var created = await trainer.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("Leading Without Fear", topics: ["Leadership"]));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var trainingId = await created.Content.ReadFromJsonAsync<Guid>();

        var facets = await WaitForFacetsAsync(holds: "Leadership");
        facets.Should().ContainKey("Leadership").WhoseValue.Should().BeGreaterThanOrEqualTo(1);

        (await trainer.PostAsync($"/Training/{trainingId}/unpublish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await WaitForFacetsAsync(holdsNot: "Leadership")).Should().NotContainKey("Leadership",
            "a facet nothing offered declares is absent rather than zero (ADR 0069)");
    }

    /// <summary>
    /// Searching a shelf, answers only trainings filed under it.
    /// </summary>
    /// <remarks>
    /// Both trainings are waited into the unfiltered catalog first, so the absence the filtered
    /// read asserts is a refusal rather than an index that has not caught up yet.
    /// </remarks>
    [Fact]
    public async Task SearchingAShelf_AnswersOnlyTrainingsFiledUnderIt()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var design = await trainer.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("Sketching Interfaces", topics: ["Design"]));
        design.StatusCode.Should().Be(HttpStatusCode.Created);
        var designId = await design.Content.ReadFromJsonAsync<Guid>();

        var marketing = await trainer.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid("Selling Interfaces", topics: ["Marketing"]));
        marketing.StatusCode.Should().Be(HttpStatusCode.Created);
        var marketingId = await marketing.Content.ReadFromJsonAsync<Guid>();

        (await WaitForCatalogAsync("interfaces", holds: designId)).Should().Contain(designId);
        (await WaitForCatalogAsync("interfaces", holds: marketingId)).Should().Contain(marketingId);

        var anonymous = Factory.CreateClient();
        var page = await anonymous.GetAsync(
            "/Catalog/trainings?term=interfaces&topic=Design&page=1&pageSize=50");
        page.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await page.Content.ReadFromJsonAsync<JsonElement>();
        var shelf = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();

        shelf.Should().Contain(designId).And.NotContain(marketingId);
    }

    /// <summary>
    /// An unknown topic, is refused at the door.
    /// </summary>
    /// <remarks>
    /// <c>[KnownTopic]</c> answers at model binding, before anything asks the index — a topic the
    /// domain does not spell names no shelf, and refusing says more than an empty page would
    /// (ADR 0069).
    /// </remarks>
    [Fact]
    public async Task AnUnknownTopic_IsRefusedAtTheDoor()
    {
        var anonymous = Factory.CreateClient();

        var page = await anonymous.GetAsync("/Catalog/trainings?topic=Astrology&page=1&pageSize=10");

        page.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The newest order, answers the youngest training first, to a caller with no token.
    /// </summary>
    /// <remarks>
    /// The whole chain of ADR 0071 in one fact: three creations commit their facts, the index
    /// learns each training's own age through the outbox, and <c>?sort=newest</c> walks them
    /// youngest first — where the default order would answer them alphabetically, which the same
    /// page proves by asking both ways.
    /// </remarks>
    [Fact]
    public async Task TheNewestOrder_AnswersTheYoungestTrainingFirst_ToACallerWithNoToken()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var oldest = await CreatedAsync(trainer, "Chronology Alpha");
        var middle = await CreatedAsync(trainer, "Chronology Beta");
        var youngest = await CreatedAsync(trainer, "Chronology Gamma");

        (await WaitForCatalogAsync("chronology", holds: oldest)).Should().Contain(oldest);
        (await WaitForCatalogAsync("chronology", holds: middle)).Should().Contain(middle);
        (await WaitForCatalogAsync("chronology", holds: youngest)).Should().Contain(youngest);

        var anonymous = Factory.CreateClient();

        var newest = await IdentifiersOfAsync(
            anonymous, "/Catalog/trainings?term=chronology&sort=newest&page=1&pageSize=50");
        newest.Should().ContainInOrder(youngest, middle, oldest);

        // The default order is the title's, and these titles were named to disagree with the
        // clock — so the same page answering both ways is the proof that the sort did something.
        var byTitle = await IdentifiersOfAsync(
            anonymous, "/Catalog/trainings?term=chronology&page=1&pageSize=50");
        byTitle.Should().ContainInOrder(oldest, middle, youngest);
    }

    /// <summary>
    /// An unknown sort, is refused at the door.
    /// </summary>
    /// <remarks>
    /// <c>[KnownSort]</c> answers at model binding, before anything asks the index — the catalog
    /// publishes two orders, and a name outside the set is a question rather than a preference
    /// (ADR 0071).
    /// </remarks>
    [Fact]
    public async Task AnUnknownSort_IsRefusedAtTheDoor()
    {
        var anonymous = Factory.CreateClient();

        var page = await anonymous.GetAsync("/Catalog/trainings?sort=fanciest&page=1&pageSize=10");

        page.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The catalogs pages, tile the offered list, without a row appearing twice or never.
    /// </summary>
    /// <remarks>
    /// The property ADR 0001 promises, asserted where a visitor meets it: over the anonymous
    /// endpoint, after the outbox has fed the index. The five trainings are waited in one by one
    /// first, so a short page means a wrong walk rather than an index that has not caught up.
    /// </remarks>
    [Fact]
    public async Task TheCatalogsPages_TileTheOfferedList_WithoutARowAppearingTwiceOrNever()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var expected = new List<Guid>();

        for (var index = 1; index <= 5; index++)
        {
            expected.Add(await CreatedAsync(trainer, $"Tiling Walk {index}"));
        }

        foreach (var trainingId in expected)
        {
            (await WaitForCatalogAsync("tiling", holds: trainingId)).Should().Contain(trainingId);
        }

        var anonymous = Factory.CreateClient();
        var walked = new List<Guid>();

        for (var page = 1; page <= 3; page++)
        {
            var response = await anonymous.GetAsync(
                $"/Catalog/trainings?term=tiling&page={page}&pageSize=2");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            body.GetProperty("totalCount").GetInt32().Should().Be(5);
            body.GetProperty("totalPages").GetInt32().Should().Be(3);
            body.GetProperty("hasNextPage").GetBoolean().Should().Be(page < 3);

            walked.AddRange(body.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid()));
        }

        walked.Should().OnlyHaveUniqueItems("a row on two pages is the tie-break failing");
        walked.Should().BeEquivalentTo(
            expected,
            options => options,
            "a row on no page is the same failure from the other side");
    }

    /// <summary>
    /// A page size beyond the cap, is refused rather than silently narrowed.
    /// </summary>
    /// <remarks>
    /// The bound ADR 0029 publishes, asserted on the public endpoint too: the contract is shared,
    /// and a cap enforced on one listing and forgotten on another would be two contracts wearing
    /// one name.
    /// </remarks>
    [Fact]
    public async Task APageSizeBeyondTheCap_IsRefusedRatherThanSilentlyNarrowed()
    {
        var anonymous = Factory.CreateClient();

        var refused = await anonymous.GetAsync(
            $"/Catalog/trainings?pageSize={PageRequest.MaxPageSize + 1}");

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A search with no paging at all, answers the default page.
    /// </summary>
    /// <remarks>
    /// An unpaged call must not mean an unbounded read (ADR 0029): the absent contract binds to
    /// null and the mapping applies the defaults, which this pins over the wire rather than in a
    /// unit test's memory.
    /// </remarks>
    [Fact]
    public async Task ASearchWithNoPagingAtAll_AnswersTheDefaultPage()
    {
        var anonymous = Factory.CreateClient();

        var response = await anonymous.GetAsync("/Catalog/trainings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(PageRequest.DefaultPageSize);
    }

    /// <summary>Creates one published training and answers its identifier.</summary>
    private static async Task<Guid> CreatedAsync(HttpClient trainer, string title)
    {
        var created = await trainer.PostAsJsonAsync("/Training", TrainingRequests.Valid(title));

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        return await created.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>The identifiers of one page, in the order the server answered them.</summary>
    private static async Task<IReadOnlyList<Guid>> IdentifiersOfAsync(HttpClient client, string address)
    {
        var response = await client.GetAsync(address);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return [.. body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())];
    }

    private async Task<IReadOnlyDictionary<string, int>> WaitForFacetsAsync(
        string? holds = null,
        string? holdsNot = null)
    {
        var timeout = TimeSpan.FromSeconds(15);
        var started = DateTime.UtcNow;
        var anonymous = Factory.CreateClient();
        IReadOnlyDictionary<string, int> lastSeen = new Dictionary<string, int>();

        while (DateTime.UtcNow - started < timeout)
        {
            var response = await anonymous.GetAsync("/Catalog/topics");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            lastSeen = body.GetProperty("topics").EnumerateArray().ToDictionary(
                facet => facet.GetProperty("topic").GetString()!,
                facet => facet.GetProperty("offeredCount").GetInt32());

            if ((holds is null || lastSeen.ContainsKey(holds))
                && (holdsNot is null || !lastSeen.ContainsKey(holdsNot)))
            {
                return lastSeen;
            }

            await Task.Delay(100);
        }

        return lastSeen;
    }

    /// <summary>
    /// Polls the public catalog until it holds — or stops holding — the training, and answers the
    /// identifiers of the page it last read.
    /// </summary>
    /// <remarks>
    /// Exactly one of the two expectations is given by each caller; the other is
    /// <see langword="null"/>. Answering the page rather than a boolean is what lets the caller
    /// assert in its own words and read a useful failure when the index never converged.
    /// </remarks>
    private async Task<IReadOnlyList<Guid>> WaitForCatalogAsync(
        string term,
        Guid? holds = null,
        Guid? holdsNot = null)
    {
        var timeout = TimeSpan.FromSeconds(15);
        var started = DateTime.UtcNow;
        var anonymous = Factory.CreateClient();
        IReadOnlyList<Guid> lastSeen = [];

        while (DateTime.UtcNow - started < timeout)
        {
            var page = await anonymous.GetAsync($"/Catalog/trainings?term={term}&page=1&pageSize=50");
            page.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await page.Content.ReadFromJsonAsync<JsonElement>();
            lastSeen = [.. body.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid())];

            if ((holds is null || lastSeen.Contains(holds.Value))
                && (holdsNot is null || !lastSeen.Contains(holdsNot.Value)))
            {
                return lastSeen;
            }

            await Task.Delay(100);
        }

        return lastSeen;
    }
}
