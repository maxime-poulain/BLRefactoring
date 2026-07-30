using System.Net;
using System.Net.Http.Headers;
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

    // -- Edit own profile --

    [Fact]
    public async Task EditMe_Authenticated_Returns200WithUpdatedProfile()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);

        var response = await client.PutAsJsonAsync("/Trainer/me", new TrainerEditionRequest
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "edited.profile@example.com",
            Bio = "A freshly written bio."
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<TrainerDto>();
        dto!.Firstname.Should().Be("Edited");
        dto.Lastname.Should().Be("Profile");
        dto.ContactEmail.Should().Be("edited.profile@example.com");
        dto.Bio.Should().Be("A freshly written bio.");
    }

    [Fact]
    public async Task EditMe_ChangedContactEmail_LeavesTheAccountLoginUntouched()
    {
        var client = factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var token = await AuthHelper.LoginAsync(client, request.Username, request.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync("/Trainer/me", new TrainerEditionRequest
        {
            Firstname = "Test",
            Lastname = "User",
            ContactEmail = "a.completely.different@example.com"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The contact email is a business attribute, not a credential: signing in
        // still works with the original account credentials.
        var freshClient = factory.CreateClient();
        var act = async () => await AuthHelper.LoginAsync(freshClient, request.Username, request.Password);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EditMe_InvalidContactEmail_Returns400()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(factory);

        var response = await client.PutAsJsonAsync("/Trainer/me", new TrainerEditionRequest
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "not-an-email"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EditMe_NoToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/Trainer/me", new TrainerEditionRequest
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "edited.profile@example.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
