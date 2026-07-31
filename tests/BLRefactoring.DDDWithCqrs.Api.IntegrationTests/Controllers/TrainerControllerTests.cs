using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using BLRefactoring.Shared.Api.Contracts.Trainers;
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
    private static EditTrainerRequestHttp ValidEdition(string firstname = "Edited", string lastname = "Profile") => new()
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
        var trainers = await response.Content.ReadFromJsonAsync<List<TrainerResponseHttp>>();
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
        var me = (await client.GetFromJsonAsync<TrainerResponseHttp>("/Trainer/me"))!;

        var response = await client.GetAsync($"/Trainer/{me.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull("the caller needs it to edit conditionally later");
        var dto = await response.Content.ReadFromJsonAsync<TrainerResponseHttp>();
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
        var dto = await response.Content.ReadFromJsonAsync<TrainerResponseHttp>();
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

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerRequestHttp
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

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerRequestHttp
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

        var reread = await client.GetFromJsonAsync<TrainerResponseHttp>("/Trainer/me");
        reread!.Firstname.Should().Be("First", "the second edit must not have overwritten the first");
    }

    // -- Delete --

    [Fact]
    public async Task Delete_IsNotExposed_OnAnyRoute()
    {
        // Removing a trainer is an administrative decision, and no role is entitled to it yet, so
        // the API exposes nothing at all — neither on `me` nor on an identifier. Both used to
        // answer 204. This test is what keeps self-deletion from creeping back in: 405 rather
        // than 404, because the matching GET routes still exist.
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var me = (await client.GetFromJsonAsync<TrainerResponseHttp>("/Trainer/me"))!;

        var onMe = await client.DeleteAsync("/Trainer/me");
        var onIdentifier = await client.DeleteAsync($"/Trainer/{me.Id}");

        onMe.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        onIdentifier.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        (await client.GetAsync("/Trainer/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
