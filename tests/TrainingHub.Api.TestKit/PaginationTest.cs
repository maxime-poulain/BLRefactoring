using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Pagination;
using TrainingHub.Shared.Api.Contracts.Trainings;
using TrainingHub.Shared.Common.Pagination;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// Paging over <c>/Training/my-trainings</c>, against a real database, on both hosts.
/// </summary>
/// <remarks>
/// These assertions only mean something over SQL Server. Whether two pages overlap depends on the
/// order the server returns rows in, and that is exactly what an in-memory provider will not
/// reproduce: it happens to preserve insertion order, so a query missing its <c>ORDER BY</c> looks
/// correct there and loses rows in production.
/// <para>
/// This suite lived in the CQRS suite alone while only that host paged, and its address was the
/// asymmetry made visible. Both hosts answer the same page envelope now (ADR 0029), and they reach
/// it by different reads — a projection into columns on one side, a repository question answering
/// aggregates on the other — so running one set of assertions against both is not a convenience:
/// it is the only way to state that two implementations keep one promise.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class PaginationTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource
{
    private static async Task CreateTrainingsAsync(HttpClient client, int count)
    {
        for (var index = 0; index < count; index++)
        {
            // Created in a tight loop, so several land within one tick of the system clock and
            // share a CreatedOn — each POST is its own SaveChanges and its own clock reading, and
            // neither helps. That is precisely the case the identifier tie-break exists for.
            var response = await client.PostAsJsonAsync("/Training", TrainingRequests.Valid($"Paged training {index:D2}"));
            response.EnsureSuccessStatusCode();
        }
    }

    private static async Task<PagedResponseHttp<TrainingResponseHttp>> GetPageAsync(
        HttpClient client, int page, int pageSize)
    {
        var response = await client.GetAsync($"/Training/my-trainings?page={page}&pageSize={pageSize}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PagedResponseHttp<TrainingResponseHttp>>())!;
    }

    /// <summary>
    /// Walking every page, returns each item exactly once.
    /// </summary>
    [Fact]
    public async Task WalkingEveryPage_ReturnsEachItemExactlyOnce()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        await CreateTrainingsAsync(client, count: 5);

        var first = await GetPageAsync(client, page: 1, pageSize: 2);
        var second = await GetPageAsync(client, page: 2, pageSize: 2);
        var third = await GetPageAsync(client, page: 3, pageSize: 2);

        first.TotalCount.Should().Be(5);
        first.TotalPages.Should().Be(3);
        first.Items.Should().HaveCount(2);
        second.Items.Should().HaveCount(2);
        third.Items.Should().HaveCount(1);

        // The property that matters, and the one an unordered query silently breaks.
        var walked = first.Items.Concat(second.Items).Concat(third.Items).Select(t => t.Id).ToList();
        walked.Should().OnlyHaveUniqueItems();
        walked.Should().HaveCount(5);
    }

    /// <summary>
    /// Metadata, describes where the caller is.
    /// </summary>
    [Fact]
    public async Task Metadata_DescribesWhereTheCallerIs()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        await CreateTrainingsAsync(client, count: 3);

        var middle = await GetPageAsync(client, page: 2, pageSize: 1);

        middle.Page.Should().Be(2);
        middle.PageSize.Should().Be(1);
        middle.HasPreviousPage.Should().BeTrue();
        middle.HasNextPage.Should().BeTrue();
    }

    /// <summary>
    /// No paging asked, still returns a page.
    /// </summary>
    [Fact]
    public async Task NoPagingAsked_StillReturnsAPage()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        await CreateTrainingsAsync(client, count: 2);

        var response = await client.GetAsync("/Training/my-trainings");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content.ReadFromJsonAsync<PagedResponseHttp<TrainingResponseHttp>>())!;

        // An unpaged call must not mean an unbounded read: the default applies.
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(PageRequest.DefaultPageSize);
    }

    /// <summary>
    /// Page size beyond the cap, is rejected, naming the parameter.
    /// </summary>
    [Fact]
    public async Task PageSizeBeyondTheCap_IsRejected_NamingTheParameter()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.GetAsync($"/Training/my-trainings?pageSize={PageRequest.MaxPageSize + 1}");

        // Rejected rather than clamped, because at this point a caller asked for something the API
        // does not serve and deserves to be told which parameter is wrong.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey(nameof(PaginationRequestHttp.PageSize));
    }
}
