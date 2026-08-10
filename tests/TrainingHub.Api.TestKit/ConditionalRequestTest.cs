using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Contracts.Trainings;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The conditional-request contract over HTTP: the version a read answers, and the one an edit
/// hands back.
/// </summary>
/// <remarks>
/// The two facts here are the dance ADR 0010 promises. The ETag a read answers is what makes a
/// conditional edit possible at all, and the ETag an edit answers is what keeps the next edit
/// possible without another read. Both lived in the CQRS suite alone, so the layered host's half
/// of the promise was asserted by nothing — the same one-suite asymmetry the auth tests carried
/// before they were merged into one base.
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class ConditionalRequestTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource
{
    /// <summary>
    /// Get me, authenticated, returns 200 with ETag.
    /// </summary>
    [Fact]
    public async Task GetMe_Authenticated_Returns200WithETag()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.GetAsync("/Trainer/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull("the caller needs it to edit conditionally later");
        // The trainer registered a line above is the one served — the endpoint takes no identifier,
        // so this is the only trainer it can answer with.
        var dto = await response.Content.ReadFromJsonAsync<TrainerHttpResponse>();
        dto!.Id.Should().NotBeEmpty();
    }

    /// <summary>
    /// What republishing the version is actually for.
    /// </summary>
    /// <remarks>
    /// The previous behavior — a bare 200 — made this sequence impossible: the caller's only
    /// version was the one the first edit had just replaced, so the second attempt could only be a
    /// 412. Correct, and useless. An extra GET was the sole way forward.
    /// </remarks>
    [Fact]
    public async Task Edit_TwiceInARow_SucceedsUsingTheVersionTheFirstEditReturned()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var entityTag = await client.GetETagAsync($"/Training/{trainingId}");

        var first = await client.PutWithIfMatchAsync(
            $"/Training/{trainingId}", ValidEdition("First Edit"), entityTag);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PutWithIfMatchAsync(
            $"/Training/{trainingId}", ValidEdition("Second Edit"), first.Headers.ETag!.ToString());

        second.StatusCode.Should().Be(HttpStatusCode.OK, "the first edit handed back a current version");

        var reread = await client.GetFromJsonAsync<TrainingHttpResponse>($"/Training/{trainingId}");
        reread!.Title.Should().Be("Second Edit");
    }

    private static EditTrainingHttpRequest ValidEdition(string title) => new()
    {
        Title = title,
        Description = "Updated description for the training",
        Prerequisites = "Updated prerequisites",
        AcquiredSkills = "Updated acquired skills",
        Topics = ["Design"]
    };

    private static async Task<Guid> CreateTrainingAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/Training", TrainingRequests.Valid($"Training {Guid.NewGuid():N}"[..25]));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>();
    }
}
