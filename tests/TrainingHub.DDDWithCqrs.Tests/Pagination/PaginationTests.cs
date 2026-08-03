using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetMine;
using TrainingHub.DDDWithCqrs.Application.Pagination;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Pagination;

/// <summary>
/// The arithmetic and the bounds of the query side's paging.
/// </summary>
public sealed class PaginationTests
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
    /// Page size, is clamped to the cap.
    /// </summary>
    [Theory]
    [InlineData(0, PagedQuery.DefaultPageSize)]
    [InlineData(-5, PagedQuery.DefaultPageSize)]
    [InlineData(1, 1)]
    [InlineData(PagedQuery.MaxPageSize, PagedQuery.MaxPageSize)]
    [InlineData(1_000_000, PagedQuery.MaxPageSize)]
    public void PageSize_IsClampedToTheCap(int requested, int expected)
    {
        // The cap is the whole point: without it, a single request restores the unbounded read
        // this work removed. A query built in code is capped just like one built from a request.
        var query = new GetMyTrainingsQuery { PageSize = requested };

        query.PageSize.Should().Be(expected);
    }

    /// <summary>
    /// Page, never goes below one.
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(7, 7)]
    public void Page_NeverGoesBelowOne(int requested, int expected)
        => new GetMyTrainingsQuery { Page = requested }.Page.Should().Be(expected);

    /// <summary>
    /// By default, a query is paged.
    /// </summary>
    [Fact]
    public void ByDefault_AQueryIsPaged()
    {
        // A list query that forgot to be paged would be the one unbounded read left.
        var query = new GetMyTrainingsQuery();

        query.Page.Should().Be(1);
        query.PageSize.Should().Be(PagedQuery.DefaultPageSize);
    }
}
