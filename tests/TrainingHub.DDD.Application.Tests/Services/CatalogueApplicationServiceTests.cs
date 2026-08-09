using AwesomeAssertions;
using Moq;
using TrainingHub.DDD.Application.Services.CatalogueServices;
using TrainingHub.Shared.Application.Catalogue;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Common.Pagination;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.Services;

/// <summary>
/// Behaviour covered for <c>CatalogueApplicationService</c>.
/// </summary>
/// <remarks>
/// The layered half of the catalogue search, and the twin of <c>SearchCatalogueQueryHandler</c>'s
/// facts on purpose: what ADR 0029 asks of the two hosts is that they answer the same question the
/// same way, and over a read model that means arriving at the same port with the same arguments.
/// </remarks>
public sealed class CatalogueApplicationServiceTests
{
    private readonly Mock<ITrainingSearchQuery> _trainingSearch = new();
    private readonly Mock<ICatalogueDetailQuery> _catalogueDetail = new();

    /// <summary>
    /// Search async, a term and a page, asks the index for exactly those.
    /// </summary>
    [Fact]
    public async Task SearchAsync_ATermAndAPage_AsksTheIndexForExactlyThose()
    {
        var paging = new PageRequest { Page = 2, PageSize = 10 };

        _trainingSearch
            .Setup(search => search.SearchAsync(It.IsAny<string?>(), It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CatalogueTrainingDto>([], 2, 10, 0));

        var sut = new CatalogueApplicationService(_trainingSearch.Object, _catalogueDetail.Object);

        await sut.SearchAsync("domain", paging);

        _trainingSearch.Verify(
            search => search.SearchAsync("domain", paging, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Search async, the page the index answered, hands it back untouched.
    /// </summary>
    /// <remarks>
    /// No mapping happens here, unlike every other listing on this stack: the index already answers
    /// the read model, so there are no aggregates to turn into one (ADR 0059).
    /// </remarks>
    [Fact]
    public async Task SearchAsync_ThePageTheIndexAnswered_HandsItBackUntouched()
    {
        var answered = new PagedResult<CatalogueTrainingDto>(
            [new CatalogueTrainingDto { Id = Guid.NewGuid(), TrainerId = Guid.NewGuid(), Title = "Found" }],
            Page: 1,
            PageSize: 20,
            TotalCount: 1);

        _trainingSearch
            .Setup(search => search.SearchAsync(It.IsAny<string?>(), It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answered);

        var sut = new CatalogueApplicationService(_trainingSearch.Object, _catalogueDetail.Object);

        var page = await sut.SearchAsync(term: null, new PageRequest());

        page.Should().BeSameAs(answered);
    }

    /// <summary>
    /// Find offered async, an identifier, asks the detail port for exactly it.
    /// </summary>
    /// <remarks>
    /// And asks the <em>detail</em> port rather than the search one. The two questions look alike
    /// enough to be answered from one place, and the whole of ADR 0062 is the argument that they
    /// must not be: a search reads the index, a reading composes the index and the write model.
    /// </remarks>
    [Fact]
    public async Task FindOfferedAsync_AnIdentifier_AsksTheDetailPortForExactlyIt()
    {
        var trainingId = Guid.CreateVersion7();

        var sut = new CatalogueApplicationService(_trainingSearch.Object, _catalogueDetail.Object);

        await sut.FindOfferedAsync(trainingId);

        _catalogueDetail.Verify(
            detail => detail.FindOfferedAsync(trainingId, It.IsAny<CancellationToken>()),
            Times.Once);

        _trainingSearch.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Find offered async, nothing on offer, hands the nothing back.
    /// </summary>
    /// <remarks>
    /// The absence travels rather than becoming a refusal here: what a missing training means to a
    /// caller is the action's decision, and it answers 404 (ADR 0055).
    /// </remarks>
    [Fact]
    public async Task FindOfferedAsync_NothingOnOffer_HandsTheNothingBack()
    {
        _catalogueDetail
            .Setup(detail => detail.FindOfferedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogueTrainingDetailDto?)null);

        var sut = new CatalogueApplicationService(_trainingSearch.Object, _catalogueDetail.Object);

        var offered = await sut.FindOfferedAsync(Guid.CreateVersion7());

        offered.Should().BeNull();
    }
}
