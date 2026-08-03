using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using TrainingHub.Shared.Api.Contracts.Trainings;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="DDD.Api.Controller.TrainingController"/>.
/// Validates HTTP endpoints for training CRUD operations including authorization.
/// </summary>
[Collection("Api")]
public sealed class TrainingControllerTests(ApiFactory factory) : IntegrationTest(factory)
{
    // One definition of "a valid training", in the test kit, rather than the five copies of the
    // same object literal this suite and its twin used to carry between them.
    private static CreateTrainingRequestHttp CreateValidTrainingRequest(string? title = null) =>
        TrainingRequests.Valid(title ?? $"Training {Guid.NewGuid():N}"[..25]);

    // -- Create --

    /// <summary>
    /// Create, valid data, returns 201.
    /// </summary>
    [Fact]
    public async Task Create_ValidData_Returns201()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var request = CreateValidTrainingRequest();

        var response = await client.PostAsJsonAsync("/Training", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // ADR 0011 cites this endpoint as the precedent for answering a creation with the address
        // of what was created, and nothing asserted the header it names.
        var trainingId = await response.Content.ReadFromJsonAsync<Guid>();
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.AbsolutePath.Should().Be($"/Training/{trainingId}");
    }

    /// <summary>
    /// Create, invalid data, returns 400.
    /// </summary>
    [Fact]
    public async Task Create_InvalidData_Returns400()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var request = new CreateTrainingRequestHttp
        {
            Title = "ab",
            Description = "",
            Prerequisites = "",
            AcquiredSkills = "",
            Topics = []
        };

        var response = await client.PostAsJsonAsync("/Training", request);

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
        var first = await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest(title));
        first.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest(title));

        // Title uniqueness per trainer is the one rule the aggregate cannot settle alone: it
        // crosses the whole stack, from IUniquenessTitleChecker down to the unique index. The
        // CQRS suite asserted it; this host was taking it on trust.
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Create, same title for another trainer, returns 201.
    /// </summary>
    [Fact]
    public async Task Create_SameTitleForAnotherTrainer_Returns201()
    {
        const string title = "A Shared Title";
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        (await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest(title)))
            .EnsureSuccessStatusCode();

        // The rule is scoped to a trainer, not global: the unique index is on (TrainerId, Title).
        var otherClient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var response = await otherClient.PostAsJsonAsync("/Training", CreateValidTrainingRequest(title));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Create, no token, returns 401.
    /// </summary>
    [Fact]
    public async Task Create_NoToken_Returns401()
    {
        var client = Factory.CreateClient();
        var request = CreateValidTrainingRequest();

        var response = await client.PostAsJsonAsync("/Training", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -- GetById --

    /// <summary>
    /// Get by id, existing, returns 200.
    /// </summary>
    [Fact]
    public async Task GetById_Existing_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var createResponse = await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var response = await client.GetAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<TrainingResponseHttp>();
        dto!.Id.Should().Be(trainingId);
    }

    /// <summary>
    /// Get by id, unknown, returns 404.
    /// </summary>
    [Fact]
    public async Task GetById_Unknown_Returns404()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.GetAsync($"/Training/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -- Edit --

    /// <summary>
    /// Edit, as owner, returns 200.
    /// </summary>
    [Fact]
    public async Task Edit_AsOwner_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var createResponse = await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var editRequest = new EditTrainingRequestHttp
        {
            Title = $"Updated {Guid.NewGuid():N}"[..25],
            Description = "Updated description for the training",
            Prerequisites = "Updated prerequisites",
            AcquiredSkills = "Updated acquired skills",
            Topics = ["Design"]
        };

        var entityTag = await client.GetETagAsync($"/Training/{trainingId}");
        var response = await client.PutWithIfMatchAsync($"/Training/{trainingId}", editRequest, entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull("the caller needs the new version to edit again");
    }

    /// <summary>
    /// Edit, without if match, returns 428.
    /// </summary>
    [Fact]
    public async Task Edit_WithoutIfMatch_Returns428()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var createResponse = await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var response = await client.PutAsJsonAsync($"/Training/{trainingId}", new EditTrainingRequestHttp
        {
            Title = $"Updated {Guid.NewGuid():N}"[..25],
            Description = "Updated description for the training",
            Prerequisites = "Updated prerequisites",
            AcquiredSkills = "Updated acquired skills",
            Topics = ["Design"]
        });

        // An unconditional write would let the caller overwrite changes they never saw.
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    /// <summary>
    /// Edit, with stale if match, returns 412 and keeps the first edit.
    /// </summary>
    [Fact]
    public async Task Edit_WithStaleIfMatch_Returns412AndKeepsTheFirstEdit()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var createResponse = await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Both callers read the same version, as two users would from their form.
        var staleTag = await client.GetETagAsync($"/Training/{trainingId}");

        var firstTitle = $"First {Guid.NewGuid():N}"[..25];
        var first = await client.PutWithIfMatchAsync($"/Training/{trainingId}", new EditTrainingRequestHttp
        {
            Title = firstTitle,
            Description = "The edit that got there first",
            Prerequisites = "Updated prerequisites",
            AcquiredSkills = "Updated acquired skills",
            Topics = ["Design"]
        }, staleTag);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PutWithIfMatchAsync($"/Training/{trainingId}", new EditTrainingRequestHttp
        {
            Title = $"Second {Guid.NewGuid():N}"[..25],
            Description = "The edit that must be refused",
            Prerequisites = "Updated prerequisites",
            AcquiredSkills = "Updated acquired skills",
            Topics = ["Design"]
        }, staleTag);

        second.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var reread = await client.GetAsync($"/Training/{trainingId}");
        var training = await reread.Content.ReadFromJsonAsync<TrainingResponseHttp>();
        training!.Title.Should().Be(firstTitle, "the second edit must not have overwritten the first");
    }

    /// <summary>
    /// Edit, as non owner, returns 403.
    /// </summary>
    [Fact]
    public async Task Edit_AsNonOwner_Returns403()
    {
        var ownerClient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var createResponse = await ownerClient.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var otherClient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var editRequest = new EditTrainingRequestHttp
        {
            Title = $"Hacked {Guid.NewGuid():N}"[..25],
            Description = "Should not be allowed",
            Prerequisites = "N/A",
            AcquiredSkills = "N/A",
            Topics = ["Programming"]
        };

        // With a valid If-Match, deliberately. Without one the request is refused for the missing
        // precondition and the 403 proves only that authorization runs before that check — it would
        // still pass with the ownership rule deleted.
        //
        // Read by the owner, because the intruder can no longer obtain it: the read by identifier
        // answers them 404 now. Handing them a version they could not have fetched is what keeps
        // this test about the write policy rather than about the read that precedes it.
        var entityTag = await ownerClient.GetETagAsync($"/Training/{trainingId}");
        var response = await otherClient.PutWithIfMatchAsync(
            $"/Training/{trainingId}", editRequest, entityTag);

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
        var createResponse = await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var response = await client.DeleteAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The status says accepted; this says gone. A delete that answers 204 and keeps the row
        // is the failure this endpoint is most likely to have.
        (await client.GetAsync($"/Training/{trainingId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Delete, as non owner, returns 403.
    /// </summary>
    [Fact]
    public async Task Delete_AsNonOwner_Returns403()
    {
        var ownerClient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var createResponse = await ownerClient.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var otherClient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await otherClient.DeleteAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Refused and still there: a 403 that had already deleted the row would be worse than a 204.
        (await ownerClient.GetAsync($"/Training/{trainingId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

}
