using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using BLRefactoring.Shared.Api.Contracts.Trainings;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The training endpoints of the CQRS host, exercised over HTTP.
/// </summary>
[Collection("Api")]
public sealed class TrainingControllerTests(ApiFactory factory) : IntegrationTest(factory)
{
    // One definition of "a valid training", in the test kit, rather than the five copies of the
    // same object literal this suite and its twin used to carry between them.
    private static CreateTrainingRequestHttp ValidCreation(string? title = null) =>
        TrainingRequests.Valid(title ?? $"Training {Guid.NewGuid():N}"[..25]);

    private static EditTrainingRequestHttp ValidEdition(string? title = null) => new()
    {
        Title = title ?? $"Updated {Guid.NewGuid():N}"[..25],
        Description = "Updated description for the training",
        Prerequisites = "Updated prerequisites",
        AcquiredSkills = "Updated acquired skills",
        Topics = ["Design"]
    };

    private static async Task<Guid> CreateTrainingAsync(HttpClient client, string? title = null)
    {
        var response = await client.PostAsJsonAsync("/Training", ValidCreation(title));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    // -- Create --

    /// <summary>
    /// Create, valid data, returns 201.
    /// </summary>
    [Fact]
    public async Task Create_ValidData_Returns201()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.PostAsJsonAsync("/Training", ValidCreation());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // On this host the identifier is the one the command generated before it was dispatched;
        // the Location has to name that one and not another.
        var trainingId = await response.Content.ReadFromJsonAsync<Guid>();
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.AbsolutePath.Should().Be($"/Training/{trainingId}");
    }

    /// <summary>
    /// Create, same title for another trainer, returns 201.
    /// </summary>
    [Fact]
    public async Task Create_SameTitleForAnotherTrainer_Returns201()
    {
        var title = $"Shared title {Guid.NewGuid():N}"[..30];

        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        (await client.PostAsJsonAsync("/Training", ValidCreation(title))).EnsureSuccessStatusCode();

        var otherClient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        // The uniqueness rule is (TrainerId, Title), and the index behind it says so. Only the
        // layered suite proved the scoping, so a rule tightened to the title alone would have
        // passed here.
        var response = await otherClient.PostAsJsonAsync("/Training", ValidCreation(title));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Create, invalid data, returns 400.
    /// </summary>
    [Fact]
    public async Task Create_InvalidData_Returns400()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.PostAsJsonAsync("/Training", new CreateTrainingRequestHttp
        {
            Title = "ab",
            Description = "",
            Prerequisites = "",
            AcquiredSkills = "",
            Topics = []
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Create, duplicate title for same trainer, returns 409.
    /// </summary>
    [Fact]
    public async Task Create_DuplicateTitleForSameTrainer_Returns409()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        const string title = "A Duplicated Title";
        await CreateTrainingAsync(client, title);

        var response = await client.PostAsJsonAsync("/Training", ValidCreation(title));

        // Title uniqueness per trainer is the one rule the aggregate cannot settle alone.
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Create, no token, returns 401.
    /// </summary>
    [Fact]
    public async Task Create_NoToken_Returns401()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/Training", ValidCreation());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -- Reads --

    /// <summary>
    /// Get by id, existing, returns 200 with ETag.
    /// </summary>
    [Fact]
    public async Task GetById_Existing_Returns200WithETag()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var response = await client.GetAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
        var dto = await response.Content.ReadFromJsonAsync<TrainingResponseHttp>();
        dto!.Id.Should().Be(trainingId);
    }

    /// <summary>
    /// Get by id, non existent, returns 404.
    /// </summary>
    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.GetAsync($"/Training/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -- Edit --

    /// <summary>
    /// Edit, as owner, returns 200 with the updated training and its new ETag.
    /// </summary>
    [Fact]
    public async Task Edit_AsOwner_Returns200WithTheUpdatedTrainingAndItsNewETag()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var entityTag = await client.GetETagAsync($"/Training/{trainingId}");
        var response = await client.PutWithIfMatchAsync($"/Training/{trainingId}", ValidEdition("Renamed Training"), entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The updated representation, read back through the query side, with the new version in
        // the ETag. This host used to answer a bare 200 with neither — so a caller who edited
        // twice in a row was guaranteed a 412 on the second attempt, holding a version the first
        // edit had just superseded, with no way forward but another GET.
        var edited = await response.Content.ReadFromJsonAsync<TrainingResponseHttp>();
        edited!.Title.Should().Be("Renamed Training");

        response.Headers.ETag.Should().NotBeNull("the caller needs the new version to edit again");
        response.Headers.ETag!.ToString().Should().NotBe(
            entityTag, "the version it replaces is no longer current");
    }

    /// <summary>
    /// What republishing the version is actually for.
    /// </summary>
    /// <remarks>
    /// The previous behaviour — a bare 200 — made this sequence impossible: the caller's only
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

        var reread = await client.GetFromJsonAsync<TrainingResponseHttp>($"/Training/{trainingId}");
        reread!.Title.Should().Be("Second Edit");
    }

    /// <summary>
    /// Edit, without if match, returns 428.
    /// </summary>
    [Fact]
    public async Task Edit_WithoutIfMatch_Returns428()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var response = await client.PutAsJsonAsync($"/Training/{trainingId}", ValidEdition());

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    /// <summary>
    /// Edit, with stale if match, returns 412 and keeps the first edit.
    /// </summary>
    [Fact]
    public async Task Edit_WithStaleIfMatch_Returns412AndKeepsTheFirstEdit()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var staleTag = await client.GetETagAsync($"/Training/{trainingId}");

        var first = await client.PutWithIfMatchAsync($"/Training/{trainingId}", ValidEdition("First Edit Wins"), staleTag);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PutWithIfMatchAsync($"/Training/{trainingId}", ValidEdition("Second Edit Lost"), staleTag);

        second.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var reread = await client.GetFromJsonAsync<TrainingResponseHttp>($"/Training/{trainingId}");
        reread!.Title.Should().Be("First Edit Wins", "the second edit must not have overwritten the first");
    }

    /// <summary>
    /// Edit, as non owner, returns 403.
    /// </summary>
    [Fact]
    public async Task Edit_AsNonOwner_Returns403()
    {
        var owner = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(owner);
        var entityTag = await owner.GetETagAsync($"/Training/{trainingId}");

        var intruder = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await intruder.PutWithIfMatchAsync($"/Training/{trainingId}", ValidEdition(), entityTag);

        // The TrainingOwner policy runs before the command is ever dispatched.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -- Delete --

    /// <summary>
    /// Delete, as owner, returns 204.
    /// </summary>
    [Fact]
    public async Task Delete_AsOwner_Returns204()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var response = await client.DeleteAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/Training/{trainingId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Delete, as non owner, returns 403.
    /// </summary>
    [Fact]
    public async Task Delete_AsNonOwner_Returns403()
    {
        var owner = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(owner);

        var intruder = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await intruder.DeleteAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
