using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Api.Contracts;
using BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using BLRefactoring.DDDWithCqrs.Application.Pagination;
using BLRefactoring.Shared.Api.Contracts.Trainings;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// Paging on the query side, against a real database.
/// </summary>
/// <remarks>
/// These assertions only mean something over SQL Server. Whether two pages overlap depends on the
/// order the server returns rows in, and that is exactly what an in-memory provider will not
/// reproduce: it happens to preserve insertion order, so a query missing its <c>ORDER BY</c> looks
/// correct there and loses rows in production.
/// </remarks>
[Collection("Api")]
public class PaginationTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static CreateTrainingRequestHttp Creation(string title) => new()
    {
        Title = title,
        Description = "A valid training description for integration testing",
        Prerequisites = "Basic programming knowledge required",
        AcquiredSkills = "Advanced design patterns mastery",
        Topics = ["Programming"]
    };

    private static async Task CreateTrainingsAsync(HttpClient client, int count)
    {
        for (var index = 0; index < count; index++)
        {
            // Created in a tight loop, so several land within one tick of the system clock and
            // share a CreatedOn — each POST is its own SaveChanges and its own clock reading, and
            // neither helps. That is precisely the case the identifier tie-break exists for.
            var response = await client.PostAsJsonAsync("/Training", Creation($"Paged training {index:D2}"));
            response.EnsureSuccessStatusCode();
        }
    }

    private static async Task<PagedResponseHttp<TrainingResponseHttp>> GetPageAsync(
        HttpClient client, int page, int pageSize)
    {
        var response = await client.GetAsync($"/Training/all?page={page}&pageSize={pageSize}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PagedResponseHttp<TrainingResponseHttp>>())!;
    }

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

    [Fact]
    public async Task NoPagingAsked_StillReturnsAPage()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        await CreateTrainingsAsync(client, count: 2);

        var response = await client.GetAsync("/Training/all");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content.ReadFromJsonAsync<PagedResponseHttp<TrainingResponseHttp>>())!;

        // An unpaged call must not mean an unbounded read: the default applies.
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(PagedQuery.DefaultPageSize);
    }

    [Fact]
    public async Task PageSizeBeyondTheCap_IsRejected_NamingTheParameter()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.GetAsync($"/Training/all?pageSize={PagedQuery.MaxPageSize + 1}");

        // Rejected rather than clamped, because at this point a caller asked for something the API
        // does not serve and deserves to be told which parameter is wrong.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey(nameof(PaginationRequestHttp.PageSize));
    }
}
