using AwesomeAssertions;
using TrainingHub.Shared.Common.Pagination;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Common.Pagination;

/// <summary>
/// The bounds of the page a caller asks for.
/// </summary>
/// <remarks>
/// The HTTP boundary rejects an out-of-range value with a message naming the field; these cover
/// the other door — a request built in code, where the cap is a policy to apply rather than an
/// error to raise. Shared by both stacks, so proven once, here in the kernel's suite.
/// </remarks>
public sealed class PageRequestTests
{
    /// <summary>
    /// Page size, is clamped to the cap.
    /// </summary>
    [Theory]
    [InlineData(0, PageRequest.DefaultPageSize)]
    [InlineData(-5, PageRequest.DefaultPageSize)]
    [InlineData(1, 1)]
    [InlineData(PageRequest.MaxPageSize, PageRequest.MaxPageSize)]
    [InlineData(1_000_000, PageRequest.MaxPageSize)]
    public void PageSize_IsClampedToTheCap(int requested, int expected)
    {
        // The cap is the whole point: without it, a single request restores the unbounded read
        // this work removed. A request built in code is capped just like one built at the boundary.
        var paging = new PageRequest { PageSize = requested };

        paging.PageSize.Should().Be(expected);
    }

    /// <summary>
    /// Page, never goes below one.
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(7, 7)]
    public void Page_NeverGoesBelowOne(int requested, int expected)
        => new PageRequest { Page = requested }.Page.Should().Be(expected);

    /// <summary>
    /// A request built with nothing, asks for the default page.
    /// </summary>
    [Fact]
    public void ARequestBuiltWithNothing_AsksForTheDefaultPage()
    {
        // What an unpaged call becomes, on either host: the default page, never the whole table.
        var paging = new PageRequest();

        paging.Page.Should().Be(1);
        paging.PageSize.Should().Be(PageRequest.DefaultPageSize);
    }
}
