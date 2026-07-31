using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Edit;
using BLRefactoring.Shared.Application.Dtos.Training;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The training endpoints of the CQRS host, exercised over HTTP.
/// </summary>
[Collection("Api")]
public class TrainingControllerTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static CreateTrainingCommand ValidCreation(string? title = null) => new()
    {
        Title = title ?? $"Training {Guid.NewGuid():N}"[..25],
        Description = "A valid training description for integration testing",
        Prerequisites = "Basic programming knowledge required",
        AcquiredSkills = "Advanced design patterns mastery",
        Topics = ["Programming"]
    };

    private static EditTrainingCommand ValidEdition(string? title = null) => new()
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

    [Fact]
    public async Task Create_ValidData_Returns201()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.PostAsJsonAsync("/Training", ValidCreation());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_InvalidData_Returns400()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.PostAsJsonAsync("/Training", new CreateTrainingCommand
        {
            Title = "ab",
            Description = "",
            Prerequisites = "",
            AcquiredSkills = "",
            Topics = []
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

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

    [Fact]
    public async Task Create_NoToken_Returns401()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/Training", ValidCreation());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -- Reads --

    [Fact]
    public async Task GetById_Existing_Returns200WithETag()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var response = await client.GetAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
        var dto = await response.Content.ReadFromJsonAsync<TrainingDto>();
        dto!.Id.Should().Be(trainingId);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.GetAsync($"/Training/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        await CreateTrainingAsync(client);

        var response = await client.GetAsync("/Training/all");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trainings = await response.Content.ReadFromJsonAsync<List<TrainingDto>>();
        trainings.Should().ContainSingle();
    }

    [Fact]
    public async Task GetByTrainerId_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        await CreateTrainingAsync(client);
        var me = (await client.GetFromJsonAsync<TrainerDto>("/Trainer/me"))!;

        var response = await client.GetAsync($"/Training/by-trainer/{me.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trainings = await response.Content.ReadFromJsonAsync<List<TrainingDto>>();
        trainings.Should().ContainSingle();
    }

    [Fact]
    public async Task GetByTopic_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        await CreateTrainingAsync(client);

        var response = await client.GetAsync("/Training/by-topic/Programming");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trainings = await response.Content.ReadFromJsonAsync<List<TrainingDto>>();
        trainings.Should().ContainSingle();
    }

    // -- Edit --

    [Fact]
    public async Task Edit_AsOwner_Returns200WithoutBody()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var entityTag = await client.GetETagAsync($"/Training/{trainingId}");
        var response = await client.PutWithIfMatchAsync($"/Training/{trainingId}", ValidEdition("Renamed Training"), entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Unlike the layered host, this one answers a bare 200: the command reports success
        // and nothing is read back. A caller who wants the new version must GET again.
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var reread = await client.GetFromJsonAsync<TrainingDto>($"/Training/{trainingId}");
        reread!.Title.Should().Be("Renamed Training");
    }

    [Fact]
    public async Task Edit_WithoutIfMatch_Returns428()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var response = await client.PutAsJsonAsync($"/Training/{trainingId}", ValidEdition());

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

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

        var reread = await client.GetFromJsonAsync<TrainingDto>($"/Training/{trainingId}");
        reread!.Title.Should().Be("First Edit Wins", "the second edit must not have overwritten the first");
    }

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

    [Fact]
    public async Task Delete_AsOwner_Returns204()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var response = await client.DeleteAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/Training/{trainingId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

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
