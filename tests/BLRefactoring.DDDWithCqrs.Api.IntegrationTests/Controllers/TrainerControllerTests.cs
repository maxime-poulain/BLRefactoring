using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Edit;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;
using BLRefactoring.Shared.Application.Dtos.Training;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The trainer endpoints of the CQRS host, exercised over HTTP.
/// </summary>
/// <remarks>
/// Reads go through query handlers that project straight from <c>TrainingContext</c>, writes
/// through commands dispatched by Mediator — neither of which the layered suite covers. The
/// request body is the command itself, so a rename on the command side breaks these tests
/// rather than silently changing the HTTP contract.
/// </remarks>
[Collection("Api")]
public class TrainerControllerTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static EditTrainerCommand ValidEdition(string firstname = "Edited", string lastname = "Profile") => new()
    {
        Firstname = firstname,
        Lastname = lastname,
        ContactEmail = "edited.profile@example.com",
        Bio = "A freshly written bio."
    };

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

    [Fact]
    public async Task GetAll_NoToken_Returns401()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/Trainer/all");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -- GetById --

    [Fact]
    public async Task GetById_ExistingTrainer_Returns200WithETag()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var me = (await client.GetFromJsonAsync<TrainerDto>("/Trainer/me"))!;

        var response = await client.GetAsync($"/Trainer/{me.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull("the caller needs it to edit conditionally later");
        var dto = await response.Content.ReadFromJsonAsync<TrainerDto>();
        dto!.Id.Should().Be(me.Id);
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

        var response = await client.PutWithIfMatchAsync("/Trainer/me", ValidEdition(), entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The command answers only whether the write succeeded; the representation comes
        // back from the query side. This asserts that read-back actually happens.
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

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerCommand
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

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerCommand
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

        var response = await client.PutAsJsonAsync("/Trainer/me", ValidEdition());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EditMe_WithoutIfMatch_Returns428()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.PutAsJsonAsync("/Trainer/me", ValidEdition());

        // An unconditional write would let the caller overwrite changes they never saw.
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task EditMe_WithStaleIfMatch_Returns412AndKeepsTheFirstEdit()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        // Both callers read the same version, as two users would from their form.
        var staleTag = await client.GetETagAsync("/Trainer/me");

        var first = await client.PutWithIfMatchAsync("/Trainer/me", ValidEdition("First", "Edit"), staleTag);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PutWithIfMatchAsync("/Trainer/me", ValidEdition("Second", "Edit"), staleTag);

        second.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var reread = await client.GetFromJsonAsync<TrainerDto>("/Trainer/me");
        reread!.Firstname.Should().Be("First", "the second edit must not have overwritten the first");
    }

    // -- Delete --

    [Fact]
    public async Task Delete_ExistingTrainer_Returns204()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var me = (await client.GetFromJsonAsync<TrainerDto>("/Trainer/me"))!;

        var response = await client.DeleteAsync($"/Trainer/{me.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.DeleteAsync($"/Trainer/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_TrainerWithTrainings_AlsoDeletesTheirTrainings()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var creation = await client.PostAsJsonAsync("/Training", new CreateTrainingCommand
        {
            Title = "A Training To Cascade",
            Description = "A valid training description for testing purposes",
            Prerequisites = "Basic programming knowledge required",
            AcquiredSkills = "Advanced design patterns mastery",
            Topics = ["Programming"]
        });
        creation.EnsureSuccessStatusCode();

        var me = (await client.GetFromJsonAsync<TrainerDto>("/Trainer/me"))!;

        var response = await client.DeleteAsync($"/Trainer/{me.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // No foreign key cascades Trainer -> Training in the database, deliberately.
        // If the training is gone, TrainerDeletedDomainEvent was dispatched during
        // SaveChanges and its handler removed it — proving the EF interceptors are wired
        // on this host too, not only on the layered one.
        var remaining = await client.GetFromJsonAsync<List<TrainingDto>>("/Training/all");
        remaining.Should().BeEmpty();
    }
}
