using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Catalog;
using TrainingHub.Shared.Api.Contracts.Mappings;
using TrainingHub.Shared.Application.Search;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Contracts;

/// <summary>
/// The one translation both hosts share for the catalog's order, and above all its default
/// (ADR 0071, ADR 0074).
/// </summary>
/// <remarks>
/// The default is the decision's whole weight: the bare address is the front door, and what it
/// answers is decided here, once, for both hosts. The TestKit proves it over the wire; this pins
/// it at the source, so a drift fails in milliseconds rather than after a container starts.
/// </remarks>
public sealed class CatalogSearchMappingsTests
{
    /// <summary>
    /// To order, no sort at all, answers newest first.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToOrder_NoSortAtAll_AnswersNewestFirst(string? sort) =>
        new CatalogSearchHttpRequest { Sort = sort }.ToOrder()
            .Should().Be(CatalogOrder.Newest,
                "the bare catalog is the front door, and it shows what recently went on offer (ADR 0074)");

    /// <summary>
    /// To order, an absent contract, answers newest first too.
    /// </summary>
    [Fact]
    public void ToOrder_AnAbsentContract_AnswersNewestFirstToo() =>
        ((CatalogSearchHttpRequest?)null).ToOrder().Should().Be(CatalogOrder.Newest);

    /// <summary>
    /// To order, a named order, answers it whatever the casing.
    /// </summary>
    [Theory]
    [InlineData("title", CatalogOrder.Title)]
    [InlineData("TITLE", CatalogOrder.Title)]
    [InlineData("newest", CatalogOrder.Newest)]
    [InlineData("Newest", CatalogOrder.Newest)]
    public void ToOrder_ANamedOrder_AnswersItWhateverTheCasing(string sort, CatalogOrder expected) =>
        new CatalogSearchHttpRequest { Sort = sort }.ToOrder().Should().Be(expected,
            "the boundary admits the names case-insensitively, so the mapping reads them the same way");
}
