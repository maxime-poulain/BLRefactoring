using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using BLRefactoring.DDD.Api.IntegrationTests.Fixtures;
using BLRefactoring.Shared.Application.Dtos.Training;
using Xunit;

namespace BLRefactoring.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="DDD.Api.Controller.TrainingController"/>.
/// Validates HTTP endpoints for training CRUD operations including authorization.
/// </summary>
[Collection("Api")]
public class TrainingControllerTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static TrainingCreationRequest CreateValidTrainingRequest(string? title = null) => new()
    {
        Title = title ?? $"Training {Guid.NewGuid():N}"[..25],
        Description = "A valid training description for integration testing",
        Prerequisites = "Basic programming knowledge required",
        AcquiredSkills = "Advanced design patterns mastery",
        Topics = ["Programming"]
    };

    // -- Create --

    [Fact]
    public async Task Create_ValidData_Returns201()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var request = CreateValidTrainingRequest();

        var response = await client.PostAsJsonAsync("/Training", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_InvalidData_Returns400()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var request = new TrainingCreationRequest
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

    [Fact]
    public async Task Create_NoToken_Returns401()
    {
        var client = Factory.CreateClient();
        var request = CreateValidTrainingRequest();

        var response = await client.PostAsJsonAsync("/Training", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -- GetById --

    [Fact]
    public async Task GetById_Existing_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var createResponse = await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var response = await client.GetAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<TrainingDto>();
        dto!.Id.Should().Be(trainingId);
    }

    // -- GetAll --

    [Fact]
    public async Task GetAll_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.GetAsync("/Training/all");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -- Edit --

    [Fact]
    public async Task Edit_AsOwner_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var createResponse = await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var editRequest = new TrainingEditionRequest
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

    [Fact]
    public async Task Edit_WithoutIfMatch_Returns428()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var createResponse = await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var response = await client.PutAsJsonAsync($"/Training/{trainingId}", new TrainingEditionRequest
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

    [Fact]
    public async Task Edit_WithStaleIfMatch_Returns412AndKeepsTheFirstEdit()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var createResponse = await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Both callers read the same version, as two users would from their form.
        var staleTag = await client.GetETagAsync($"/Training/{trainingId}");

        var firstTitle = $"First {Guid.NewGuid():N}"[..25];
        var first = await client.PutWithIfMatchAsync($"/Training/{trainingId}", new TrainingEditionRequest
        {
            Title = firstTitle,
            Description = "The edit that got there first",
            Prerequisites = "Updated prerequisites",
            AcquiredSkills = "Updated acquired skills",
            Topics = ["Design"]
        }, staleTag);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PutWithIfMatchAsync($"/Training/{trainingId}", new TrainingEditionRequest
        {
            Title = $"Second {Guid.NewGuid():N}"[..25],
            Description = "The edit that must be refused",
            Prerequisites = "Updated prerequisites",
            AcquiredSkills = "Updated acquired skills",
            Topics = ["Design"]
        }, staleTag);

        second.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var reread = await client.GetAsync($"/Training/{trainingId}");
        var training = await reread.Content.ReadFromJsonAsync<TrainingDto>();
        training!.Title.Should().Be(firstTitle, "the second edit must not have overwritten the first");
    }

    [Fact]
    public async Task Edit_AsNonOwner_Returns403()
    {
        var ownerClient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var createResponse = await ownerClient.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var otherClient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var editRequest = new TrainingEditionRequest
        {
            Title = $"Hacked {Guid.NewGuid():N}"[..25],
            Description = "Should not be allowed",
            Prerequisites = "N/A",
            AcquiredSkills = "N/A",
            Topics = ["Programming"]
        };

        var response = await otherClient.PutAsJsonAsync($"/Training/{trainingId}", editRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -- Delete --

    [Fact]
    public async Task Delete_AsOwner_Returns204()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var createResponse = await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var response = await client.DeleteAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // -- GetByTopic --

    [Fact]
    public async Task GetByTopic_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());

        var response = await client.GetAsync("/Training/by-topic/Programming");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trainings = await response.Content.ReadFromJsonAsync<List<TrainingDto>>();
        trainings.Should().NotBeEmpty();
    }

    // -- GetByTrainerId --

    [Fact]
    public async Task GetByTrainerId_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());

        var trainersResponse = await client.GetAsync("/Trainer/all");
        var trainers = await trainersResponse.Content.ReadFromJsonAsync<List<Shared.Application.Dtos.Trainer.TrainerDto>>();
        var trainerId = trainers!.First().Id;

        var response = await client.GetAsync($"/Training/by-trainer/{trainerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
