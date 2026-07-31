using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BLRefactoring.DDD.Api.IntegrationTests.Fixtures;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.Application.Dtos.Training;
using FluentAssertions;
using Xunit;

namespace BLRefactoring.DDD.Api.IntegrationTests.Controllers;

[Collection("Api")]
public class TrainerControllerTests(ApiFactory factory) : IntegrationTest(factory)
{
    // -- GetAll --

    [Fact]
    public async Task GetAll_Authenticated_Returns200()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.GetAsync("/Trainer/all");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trainers = await response.Content.ReadFromJsonAsync<List<TrainerDto>>();
        trainers.Should().NotBeNull();
    }

    // -- GetById --

    [Fact]
    public async Task GetById_ExistingTrainer_Returns200WithDto()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

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
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.GetAsync($"/Trainer/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -- Edit own profile --

    [Fact]
    public async Task EditMe_Authenticated_Returns200WithUpdatedProfile()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var entityTag = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new TrainerEditionRequest
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "edited.profile@example.com",
            Bio = "A freshly written bio."
        }, entityTag);

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
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var token = await AuthHelper.LoginAsync(client, request.Username, request.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var entityTag = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new TrainerEditionRequest
        {
            Firstname = "Test",
            Lastname = "User",
            ContactEmail = "a.completely.different@example.com"
        }, entityTag);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The contact email is a business attribute, not a credential: signing in
        // still works with the original account credentials.
        var freshClient = Factory.CreateClient();
        var act = async () => await AuthHelper.LoginAsync(freshClient, request.Username, request.Password);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EditMe_InvalidContactEmail_Returns400()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var entityTag = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new TrainerEditionRequest
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "not-an-email"
        }, entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EditMe_NoToken_Returns401()
    {
        var client = Factory.CreateClient();

        var response = await client.PutAsJsonAsync("/Trainer/me", new TrainerEditionRequest
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "edited.profile@example.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EditMe_WithoutIfMatch_Returns428()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.PutAsJsonAsync("/Trainer/me", new TrainerEditionRequest
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "edited.profile@example.com"
        });

        // An unconditional write would let the caller overwrite changes they never saw.
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task EditMe_WithStaleIfMatch_Returns412AndKeepsTheFirstEdit()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        // Both callers read the same version, as two users would from their form.
        var staleTag = await client.GetETagAsync("/Trainer/me");

        var first = await client.PutWithIfMatchAsync("/Trainer/me", new TrainerEditionRequest
        {
            Firstname = "First",
            Lastname = "Edit",
            ContactEmail = "first.edit@example.com"
        }, staleTag);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PutWithIfMatchAsync("/Trainer/me", new TrainerEditionRequest
        {
            Firstname = "Second",
            Lastname = "Edit",
            ContactEmail = "second.edit@example.com"
        }, staleTag);

        second.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var reread = await client.GetFromJsonAsync<TrainerDto>("/Trainer/me");
        reread!.Firstname.Should().Be("First", "the second edit must not have overwritten the first");
    }

    // -- Delete --

    [Fact]
    public async Task Delete_ExistingTrainer_Returns204()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var allResponse = await client.GetAsync("/Trainer/all");
        var trainers = await allResponse.Content.ReadFromJsonAsync<List<TrainerDto>>();
        var trainer = trainers!.Last();

        var response = await client.DeleteAsync($"/Trainer/{trainer.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_TrainerWithTrainings_AlsoDeletesTheirTrainings()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var creation = await client.PostAsJsonAsync("/Training", new TrainingCreationRequest
        {
            Title = "A Training To Cascade",
            Description = "A valid training description for testing purposes",
            Prerequisites = "Basic programming knowledge required",
            AcquiredSkills = "Advanced design patterns mastery",
            Topics = ["Programming"]
        });
        creation.EnsureSuccessStatusCode();

        var trainer = (await client.GetFromJsonAsync<TrainerDto>("/Trainer/me"))!;

        var response = await client.DeleteAsync($"/Trainer/{trainer.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // No foreign key cascades Trainer -> Training in the database, deliberately.
        // If the training is gone, TrainerDeletedDomainEvent was dispatched during
        // SaveChanges and its handler removed it — the one assertion that proves the
        // event pipeline is wired under test, and not just in production.
        var remaining = await client.GetFromJsonAsync<List<TrainingDto>>("/Training/all");
        remaining.Should().BeEmpty();
    }

    // -- Unauthorized --

    [Fact]
    public async Task GetAll_NoToken_Returns401()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/Trainer/all");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
