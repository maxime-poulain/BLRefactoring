using AwesomeAssertions;
using TrainingHub.Shared.Common.Pagination;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Common.Pagination;

/// <summary>
/// The arithmetic of a page, and the mapping that must not touch it.
/// </summary>
public sealed class PagedResultTests
{
    private static PagedResult<string> Page(int page, int pageSize, int totalCount)
        => new([], page, pageSize, totalCount);

    /// <summary>
    /// Total pages, counts the partial page.
    /// </summary>
    [Theory]
    [InlineData(0, 20, 1)]      // nothing matched: the caller is on page 1 of 1, not 1 of 0
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]     // the partial page counts
    [InlineData(41, 20, 3)]
    public void TotalPages_CountsThePartialPage(int totalCount, int pageSize, int expected)
        => Page(page: 1, pageSize, totalCount).TotalPages.Should().Be(expected);

    /// <summary>
    /// First page of several, has a next and no previous.
    /// </summary>
    [Fact]
    public void FirstPageOfSeveral_HasANextAndNoPrevious()
    {
        var page = Page(page: 1, pageSize: 20, totalCount: 50);

        page.HasNextPage.Should().BeTrue();
        page.HasPreviousPage.Should().BeFalse();
    }

    /// <summary>
    /// Last page, has a previous and no next.
    /// </summary>
    [Fact]
    public void LastPage_HasAPreviousAndNoNext()
    {
        var page = Page(page: 3, pageSize: 20, totalCount: 50);

        page.HasNextPage.Should().BeFalse();
        page.HasPreviousPage.Should().BeTrue();
    }

    /// <summary>
    /// Map, reshapes every item and touches nothing else.
    /// </summary>
    [Fact]
    public void Map_ReshapesEveryItem_AndTouchesNothingElse()
    {
        var page = new PagedResult<int>([1, 2, 3], Page: 2, PageSize: 3, TotalCount: 8);

        var mapped = page.Map(item => $"#{item}");

        // The layered stack rides this between a page of aggregates and a page of DTOs: if the
        // counts or the position drifted here, the pager would describe a set the caller is not
        // looking at.
        mapped.Items.Should().Equal("#1", "#2", "#3");
        mapped.Page.Should().Be(2);
        mapped.PageSize.Should().Be(3);
        mapped.TotalCount.Should().Be(8);
        mapped.TotalPages.Should().Be(page.TotalPages);
    }

    /// <summary>
    /// Map, refuses a missing projection.
    /// </summary>
    [Fact]
    public void Map_RefusesAMissingProjection()
    {
        var page = Page(page: 1, pageSize: 20, totalCount: 0);

        var act = () => page.Map<string>(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
