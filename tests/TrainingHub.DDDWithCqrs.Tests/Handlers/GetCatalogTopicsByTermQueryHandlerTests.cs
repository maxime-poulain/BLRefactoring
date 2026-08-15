using AwesomeAssertions;
using Moq;
using TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetTopics;
using TrainingHub.DDDWithCqrs.Infrastructure.Features.Catalog.GetTopics;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.Search;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behavior covered for the handler that answers the catalog's facets.
/// </summary>
/// <remarks>
/// A pass-through to the one port the index publishes, like its search sibling and for the same
/// reason (ADR 0059, ADR 0069): over a read model the two stacks have nothing to differ about, and
/// what is worth pinning is that the answer travels untouched.
/// </remarks>
public sealed class GetCatalogTopicsByTermQueryHandlerTests
{
    private readonly Mock<ITrainingSearchQuery> _trainingSearch = new();

    /// <summary>
    /// Handle, the facets the index answered, hands them back untouched.
    /// </summary>
    [Fact]
    public async Task Handle_TheFacetsTheIndexAnswered_HandsThemBackUntouched()
    {
        var answered = new List<TopicFacetDto>
        {
            new() { Topic = "Programming", OfferedCount = 3 }
        };

        _trainingSearch
            .Setup(search => search.FacetsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answered);

        var sut = new GetCatalogTopicsByTermQueryHandler(_trainingSearch.Object);

        var facets = await sut.Handle(new GetCatalogTopicsByTermQuery(), CancellationToken.None);

        facets.Should().BeSameAs(answered);
    }
}
