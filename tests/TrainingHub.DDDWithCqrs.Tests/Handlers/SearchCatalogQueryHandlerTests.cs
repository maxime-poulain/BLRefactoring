using AwesomeAssertions;
using Moq;
using TrainingHub.DDDWithCqrs.Application.Features.Catalog.Search;
using TrainingHub.DDDWithCqrs.Infrastructure.Features.Catalog.Search;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Common.Pagination;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behavior covered for <c>SearchCatalogQueryHandler</c>.
/// </summary>
/// <remarks>
/// What is worth pinning here is not a projection — this handler writes none — but the fact that the
/// query's two fields reach the index unchanged, and that nothing on the way adds a criterion of its
/// own. What the index does with them is exercised against a real database in
/// <c>TrainingSearchQueryTests</c>.
/// </remarks>
public sealed class SearchCatalogQueryHandlerTests
{
    private readonly Mock<ITrainingSearchQuery> _trainingSearch = new();

    /// <summary>
    /// Handle, a term and a page, asks the index for exactly those.
    /// </summary>
    [Fact]
    public async Task Handle_ATermAndAPage_AsksTheIndexForExactlyThose()
    {
        var paging = new PageRequest { Page = 3, PageSize = 5 };

        _trainingSearch
            .Setup(search => search.SearchAsync("event storming", "Design", paging, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CatalogTrainingDto>([], 3, 5, 0));

        var sut = new SearchCatalogQueryHandler(_trainingSearch.Object);

        await sut.Handle(
            new SearchCatalogQuery { Term = "event storming", Topic = "Design", Paging = paging },
            CancellationToken.None);

        _trainingSearch.Verify(
            search => search.SearchAsync("event storming", "Design", paging, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, the page the index answered, hands it back untouched.
    /// </summary>
    /// <remarks>
    /// The counts and the position have to cross this handler unchanged: rebuilding the envelope is
    /// how a count and its items end up describing different sets (ADR 0029).
    /// </remarks>
    [Fact]
    public async Task Handle_ThePageTheIndexAnswered_HandsItBackUntouched()
    {
        var answered = new PagedResult<CatalogTrainingDto>(
            [new CatalogTrainingDto { Id = Guid.NewGuid(), TrainerId = Guid.NewGuid(), Title = "Found" }],
            Page: 2,
            PageSize: 1,
            TotalCount: 7);

        _trainingSearch
            .Setup(search => search.SearchAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answered);

        var sut = new SearchCatalogQueryHandler(_trainingSearch.Object);

        var page = await sut.Handle(new SearchCatalogQuery(), CancellationToken.None);

        page.Should().BeSameAs(answered);
    }
}
