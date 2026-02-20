using System.Net;
using System.Net.Http.Json;
using BLRefactoring.DDD.Api.IntegrationTests.Fixtures;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using FluentAssertions;
using Xunit;

namespace BLRefactoring.DDD.Api.IntegrationTests.Controllers;

[Collection("Api")]
public class TrainerControllerTests(ApiFactory factory)
{
    // -- GetAll --

    [Fact]
    public async Task GetAll_Authenticated_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/Trainer/all");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trainers = await response.Content.ReadFromJsonAsync<List<TrainerDto>>();
        trainers.Should().NotBeNull();
    }

    // -- GetById --

    [Fact]
    public async Task GetById_ExistingTrainer_Returns200WithDto()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);

        var allResponse = await client.GetAsync("/Trainer/all");
        var trainers = await allResponse.Content.ReadFromJsonAsync<List<TrainerDto>>();
        var trainer = trainers!.First();

        var response = await client.GetAsync($"/Trainer/{trainer.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<TrainerDto>();
        dto!.Id.Should().Be(trainer.Id);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);

        var response = await client.GetAsync($"/Trainer/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -- Delete --

    [Fact]
    public async Task Delete_ExistingTrainer_Returns204()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);

        var allResponse = await client.GetAsync("/Trainer/all");
        var trainers = await allResponse.Content.ReadFromJsonAsync<List<TrainerDto>>();
        var trainer = trainers!.Last();

        var response = await client.DeleteAsync($"/Trainer/{trainer.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // -- Unauthorized --

    [Fact]
    public async Task GetAll_NoToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/Trainer/all");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
