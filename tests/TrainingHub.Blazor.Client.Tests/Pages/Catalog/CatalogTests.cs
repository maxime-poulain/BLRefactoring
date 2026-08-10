using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrainingHub.GeneratedClients;
using Xunit;
using CatalogPage = TrainingHub.Blazor.Client.Pages.Catalog.Catalog;

namespace TrainingHub.Blazor.Client.Tests.Pages.Catalog;

/// <summary>
/// Behavior covered for the public catalog's listing.
/// </summary>
/// <remarks>
/// The first screen here that renders for nobody in particular. Every other page in this suite is
/// rendered with an authentication state because the page under test demands one; this one is
/// rendered without, deliberately, and that absence is the fact <see
/// cref="Renders_WithNoAuthenticationStateAtAll_StillShowsTheCatalog"/> pins (ADR 0062).
/// </remarks>
public sealed class CatalogTests : ComponentTest
{
    private readonly Mock<ICatalogClient> _catalog = new();

    /// <summary>Catalog tests.</summary>
    public CatalogTests()
    {
        Services.AddSingleton(_catalog.Object);

        _catalog
            .Setup(client => client.SearchTrainingsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(totalCount: 0));
    }

    /// <summary>
    /// Renders, asks the server for the first page and lets it choose the size.
    /// </summary>
    [Fact]
    public void Renders_AsksTheServerForTheFirstPage_AndLetsItChooseTheSize()
    {
        Render<CatalogPage>();

        _catalog.Verify(client => client.SearchTrainingsAsync(null, null, 1, null), Times.Once);
    }

    /// <summary>
    /// Renders, with no authentication state at all, still shows the catalog.
    /// </summary>
    /// <remarks>
    /// bUnit throws when a component asks for an authentication state nobody provided, so a page
    /// that grew an <c>AuthorizeView</c> would fail here rather than quietly starting to ask who is
    /// looking. What this cannot see is an <c>[Authorize]</c> attribute — the router honors it and
    /// a directly rendered component does not — so the route's anonymity is pinned one layer out,
    /// by the proxy's rule and its three facts in <c>BffTests</c> (ADR 0062).
    /// </remarks>
    [Fact]
    public void Renders_WithNoAuthenticationStateAtAll_StillShowsTheCatalog()
    {
        var page = Render<CatalogPage>();

        page.Markup.Should().Contain("Trainings on offer");
    }

    /// <summary>
    /// Renders, a training on offer, links to its page rather than showing its identifier.
    /// </summary>
    /// <remarks>
    /// A row holds a title and nothing else, because that is all the index holds. What makes the
    /// listing usable is the link out of it (ADR 0062).
    /// </remarks>
    [Fact]
    public void Renders_ATrainingOnOffer_LinksToItsPage()
    {
        var trainingId = Guid.CreateVersion7();

        _catalog
            .Setup(client => client.SearchTrainingsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(totalCount: 1, Offered(trainingId, "Domain Driven Design")));

        var page = Render<CatalogPage>();

        page.Markup.Should().Contain("Domain Driven Design");
        page.Markup.Should().Contain($"/catalog/{trainingId}");
    }

    /// <summary>
    /// Search, a term, asks the server for it and goes back to the first page.
    /// </summary>
    /// <remarks>
    /// The page reset is the half worth pinning: a visitor on page four who narrows the search would
    /// otherwise be shown the fourth page of a shorter list, which is usually empty.
    /// </remarks>
    [Fact]
    public void Search_ATerm_AsksTheServerForItOnTheFirstPage()
    {
        var page = Render<CatalogPage>();

        page.Find("input").Input("domain");

        page.WaitForAssertion(() => _catalog.Verify(
            client => client.SearchTrainingsAsync("domain", null, 1, null),
            Times.Once));
    }

    /// <summary>
    /// Renders, the server unreachable, says so rather than showing an empty catalog.
    /// </summary>
    [Fact]
    public void Renders_TheServerUnreachable_SaysSo()
    {
        _catalog
            .Setup(client => client.SearchTrainingsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new HttpRequestException("no route to host"));

        Render<CatalogPage>();

        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("The catalog could not be loaded.");
    }

    /// <summary>
    /// Renders, topics on offer, shows one chip per facet with its count.
    /// </summary>
    /// <remarks>
    /// The facets come from their own endpoint rather than from the page of results, so the chips
    /// describe the whole catalog while the table describes the current question (ADR 0069).
    /// </remarks>
    [Fact]
    public void Renders_TopicsOnOffer_ShowsOneChipPerFacetWithItsCount()
    {
        _catalog
            .Setup(client => client.GetTopicsAsync())
            .ReturnsAsync(Facets(Facet("Programming", 3), Facet("Design", 1)));

        var page = Render<CatalogPage>();

        page.Markup.Should().Contain("Programming (3)");
        page.Markup.Should().Contain("Design (1)");
    }

    /// <summary>
    /// Clicking a topic chip, asks the server for that shelf on the first page.
    /// </summary>
    /// <remarks>
    /// The page reset is the half worth pinning, exactly as it is for the term: a visitor deep in
    /// the whole catalog who narrows to one topic would otherwise be shown a page the shorter
    /// shelf does not have.
    /// </remarks>
    [Fact]
    public void ClickingATopicChip_AsksTheServerForThatShelfOnTheFirstPage()
    {
        _catalog
            .Setup(client => client.GetTopicsAsync())
            .ReturnsAsync(Facets(Facet("Programming", 3)));

        var page = Render<CatalogPage>();

        Chip(page, "Programming").Click();

        page.WaitForAssertion(() => _catalog.Verify(
            client => client.SearchTrainingsAsync(null, "Programming", 1, null),
            Times.Once));
    }

    /// <summary>
    /// Clicking the selected chip again, lifts the filter.
    /// </summary>
    /// <remarks>
    /// A facet is a lens, not a destination: the second click asks the unfiltered question again,
    /// and the only unfiltered searches are the render's and this one.
    /// </remarks>
    [Fact]
    public void ClickingTheSelectedChipAgain_LiftsTheFilter()
    {
        _catalog
            .Setup(client => client.GetTopicsAsync())
            .ReturnsAsync(Facets(Facet("Programming", 3)));

        var page = Render<CatalogPage>();

        Chip(page, "Programming").Click();
        page.WaitForAssertion(() => Chip(page, "Programming").ClassList.Should().Contain("mud-chip-filled"));
        Chip(page, "Programming").Click();

        page.WaitForAssertion(() => _catalog.Verify(
            client => client.SearchTrainingsAsync(null, null, 1, null),
            Times.Exactly(2)));
    }

    private static AngleSharp.Dom.IElement Chip(
        IRenderedComponent<CatalogPage> page, string topic) =>
        page.FindAll(".mud-chip").Single(chip => chip.TextContent.Contains(
            topic, StringComparison.Ordinal));

    private static CatalogTopicsHttpResponse Facets(params CatalogTopicHttpResponse[] facets) =>
        new() { Topics = [.. facets] };

    private static CatalogTopicHttpResponse Facet(string topic, int offeredCount) => new()
    {
        Topic = topic,
        OfferedCount = offeredCount
    };

    private static CatalogTrainingHttpResponse Offered(Guid trainingId, string title) => new()
    {
        Id = trainingId,
        TrainerId = Guid.CreateVersion7(),
        Title = title
    };

    private static PagedHttpResponseOfCatalogTrainingHttpResponse Page(
        int totalCount,
        params CatalogTrainingHttpResponse[] items) =>
        new()
        {
            Items = [.. items],
            Page = 1,
            PageSize = 20,
            TotalCount = totalCount,
            // At least one, the way the server answers it: a page that claimed zero would make the
            // screen renumber itself to page zero on load.
            TotalPages = 1
        };
}
