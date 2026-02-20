using System.Net;
using System.Net.Http.Json;
using BLRefactoring.DDD.Api.IntegrationTests.Fixtures;
using BLRefactoring.Shared.Application.Dtos.Training;
using FluentAssertions;
using Xunit;

namespace BLRefactoring.DDD.Api.IntegrationTests.Controllers;

[Collection("Api")]
public class TrainingControllerTests(ApiFactory factory)
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
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);
        var request = CreateValidTrainingRequest();

        var response = await client.PostAsJsonAsync("/Training", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_InvalidData_Returns400()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);
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
    public async Task Create_NoToken_Returns401()
    {
        var client = factory.CreateClient();
        var request = CreateValidTrainingRequest();

        var response = await client.PostAsJsonAsync("/Training", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -- GetById --

    [Fact]
    public async Task GetById_Existing_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);
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
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/Training/all");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -- Edit --

    [Fact]
    public async Task Edit_AsOwner_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);
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

        var response = await client.PutAsJsonAsync($"/Training/{trainingId}", editRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_AsNonOwner_Returns403()
    {
        var ownerClient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);
        var createResponse = await ownerClient.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var otherClient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);
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
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);
        var createResponse = await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());
        var trainingId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var response = await client.DeleteAsync($"/Training/{trainingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // -- GetByTrainerId --

    [Fact]
    public async Task GetByTrainerId_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);
        await client.PostAsJsonAsync("/Training", CreateValidTrainingRequest());

        var trainersResponse = await client.GetAsync("/Trainer/all");
        var trainers = await trainersResponse.Content.ReadFromJsonAsync<List<Shared.Application.Dtos.Trainer.TrainerDto>>();
        var trainerId = trainers!.First().Id;

        var response = await client.GetAsync($"/Training/by-trainer/{trainerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
