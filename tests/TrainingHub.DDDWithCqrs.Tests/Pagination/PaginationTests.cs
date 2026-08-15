using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetByCurrentTrainer;
using TrainingHub.Shared.Common.Pagination;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Pagination;

/// <summary>
/// What is left of paging on the query side: that a query cannot forget it.
/// </summary>
/// <remarks>
/// The arithmetic and the bounds moved to the kernel's suite with the types they cover —
/// <c>PagedResultTests</c> and <c>PageRequestTests</c> in <c>Shared.Domain.Tests</c> — when both
/// stacks started paging (ADR 0029). What stays here is the one claim that belongs to this stack:
/// its list query carries a page by construction.
/// </remarks>
public sealed class PaginationTests
{
    /// <summary>
    /// By default, a query is paged.
    /// </summary>
    [Fact]
    public void ByDefault_AQueryIsPaged()
    {
        // A list query that forgot to be paged would be the one unbounded read left. The paging
        // is held rather than inherited since PagedQuery dissolved into the kernel's PageRequest,
        // and the default is what makes "held" safe: a query built in code with nothing named
        // asks for the default page, never for the whole table.
        var query = new GetTrainingsByCurrentTrainerQuery();

        query.Paging.Page.Should().Be(1);
        query.Paging.PageSize.Should().Be(PageRequest.DefaultPageSize);
    }

    /// <summary>
    /// The paging a query is given, is the paging it carries.
    /// </summary>
    [Fact]
    public void ThePagingAQueryIsGiven_IsThePagingItCarries()
    {
        var paging = new PageRequest { Page = 4, PageSize = 10 };

        var query = new GetTrainingsByCurrentTrainerQuery { Paging = paging };

        query.Paging.Should().BeSameAs(paging);
    }
}
