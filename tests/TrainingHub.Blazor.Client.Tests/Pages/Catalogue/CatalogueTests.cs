using AwesomeAssertions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrainingHub.GeneratedClients;
using Xunit;
using CataloguePage = TrainingHub.Blazor.Client.Pages.Catalogue.Catalogue;

namespace TrainingHub.Blazor.Client.Tests.Pages.Catalogue;

/// <summary>
/// Behaviour covered for the public catalogue's listing.
/// </summary>
/// <remarks>
/// The first screen here that renders for nobody in particular. Every other page in this suite is
/// rendered with an authentication state because the page under test demands one; this one is
/// rendered without, deliberately, and that absence is the fact <see
/// cref="Renders_WithNoAuthenticationStateAtAll_StillShowsTheCatalogue"/> pins (ADR 0062).
/// </remarks>
public sealed class CatalogueTests : ComponentTest
{
    private readonly Mock<ICatalogueClient> _catalogue = new();

    /// <summary>Catalogue tests.</summary>
    public CatalogueTests()
    {
        Services.AddSingleton(_catalogue.Object);

        _catalogue
            .Setup(client => client.SearchTrainingsAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(totalCount: 0));
    }

    /// <summary>
    /// Renders, asks the server for the first page and lets it choose the size.
    /// </summary>
    [Fact]
    public void Renders_AsksTheServerForTheFirstPage_AndLetsItChooseTheSize()
    {
        Render<CataloguePage>();

        _catalogue.Verify(client => client.SearchTrainingsAsync(null, 1, null), Times.Once);
    }

    /// <summary>
    /// Renders, with no authentication state at all, still shows the catalogue.
    /// </summary>
    /// <remarks>
    /// bUnit throws when a component asks for an authentication state nobody provided, so a page
    /// that grew an <c>AuthorizeView</c> would fail here rather than quietly starting to ask who is
    /// looking. What this cannot see is an <c>[Authorize]</c> attribute — the router honours it and
    /// a directly rendered component does not — so the route's anonymity is pinned one layer out,
    /// by the proxy's rule and its three facts in <c>BffTests</c> (ADR 0062).
    /// </remarks>
    [Fact]
    public void Renders_WithNoAuthenticationStateAtAll_StillShowsTheCatalogue()
    {
        var page = Render<CataloguePage>();

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

        _catalogue
            .Setup(client => client.SearchTrainingsAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Page(totalCount: 1, Offered(trainingId, "Domain Driven Design")));

        var page = Render<CataloguePage>();

        page.Markup.Should().Contain("Domain Driven Design");
        page.Markup.Should().Contain($"/catalogue/{trainingId}");
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
        var page = Render<CataloguePage>();

        page.Find("input").Input("domain");

        page.WaitForAssertion(() => _catalogue.Verify(
            client => client.SearchTrainingsAsync("domain", 1, null),
            Times.Once));
    }

    /// <summary>
    /// Renders, the server unreachable, says so rather than showing an empty catalogue.
    /// </summary>
    [Fact]
    public void Renders_TheServerUnreachable_SaysSo()
    {
        _catalogue
            .Setup(client => client.SearchTrainingsAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new HttpRequestException("no route to host"));

        Render<CataloguePage>();

        Shown().Should().ContainSingle()
            .Which.Message.Should().Be("The catalogue could not be loaded.");
    }

    private static CatalogueTrainingHttpResponse Offered(Guid trainingId, string title) => new()
    {
        Id = trainingId,
        TrainerId = Guid.CreateVersion7(),
        Title = title
    };

    private static PagedHttpResponseOfCatalogueTrainingHttpResponse Page(
        int totalCount,
        params CatalogueTrainingHttpResponse[] items) =>
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
