using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using TrainingHub.DDD.Api.IntegrationTests.Fixtures;
using TrainingHub.Shared.Api.Contracts.Trainers;
using Xunit;

namespace TrainingHub.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// Behaviour covered for <c>TrainerController</c>.
/// </summary>
[Collection("Api")]
public sealed class TrainerControllerTests(ApiFactory factory) : IntegrationTest(factory)
{
    // -- Edit own profile --

    /// <summary>
    /// Edit me, authenticated, returns 200 with updated profile.
    /// </summary>
    [Fact]
    public async Task EditMe_Authenticated_Returns200WithUpdatedProfile()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var entityTag = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerHttpRequest
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "edited.profile@example.com",
            Bio = "A freshly written bio."
        }, entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<TrainerHttpResponse>();
        dto!.Firstname.Should().Be("Edited");
        dto.Lastname.Should().Be("Profile");
        dto.ContactEmail.Should().Be("edited.profile@example.com");
        dto.Bio.Should().Be("A freshly written bio.");
    }

    /// <summary>
    /// Edit me, changed contact email, leaves the account login untouched.
    /// </summary>
    [Fact]
    public async Task EditMe_ChangedContactEmail_LeavesTheAccountLoginUntouched()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var token = await AuthHelper.LoginAsync(client, request.Username, request.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var entityTag = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerHttpRequest
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

    /// <summary>
    /// Edit me, invalid contact email, returns 400.
    /// </summary>
    [Fact]
    public async Task EditMe_InvalidContactEmail_Returns400()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var entityTag = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerHttpRequest
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "not-an-email"
        }, entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Edit me, no token, returns 401.
    /// </summary>
    [Fact]
    public async Task EditMe_NoToken_Returns401()
    {
        var client = Factory.CreateClient();

        var response = await client.PutAsJsonAsync("/Trainer/me", new EditTrainerHttpRequest
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "edited.profile@example.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Edit me, without if match, returns 428.
    /// </summary>
    [Fact]
    public async Task EditMe_WithoutIfMatch_Returns428()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.PutAsJsonAsync("/Trainer/me", new EditTrainerHttpRequest
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "edited.profile@example.com"
        });

        // An unconditional write would let the caller overwrite changes they never saw.
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    /// <summary>
    /// Edit me, with stale if match, returns 412 and keeps the first edit.
    /// </summary>
    [Fact]
    public async Task EditMe_WithStaleIfMatch_Returns412AndKeepsTheFirstEdit()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        // Both callers read the same version, as two users would from their form.
        var staleTag = await client.GetETagAsync("/Trainer/me");

        var first = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerHttpRequest
        {
            Firstname = "First",
            Lastname = "Edit",
            ContactEmail = "first.edit@example.com"
        }, staleTag);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerHttpRequest
        {
            Firstname = "Second",
            Lastname = "Edit",
            ContactEmail = "second.edit@example.com"
        }, staleTag);

        second.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var reread = await client.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me");
        reread!.Firstname.Should().Be("First", "the second edit must not have overwritten the first");
    }

    // -- Delete --

    /// <summary>
    /// Delete, is not exposed, on any route.
    /// </summary>
    [Fact]
    public async Task Delete_IsNotExposed_OnAnyRoute()
    {
        // Removing a trainer is an administrative decision, and no role is entitled to it yet, so
        // the API exposes nothing at all. Both `me` and an identifier used to answer 204, and this
        // test is what keeps self-deletion from creeping back in.
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var me = (await client.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!;

        var onMe = await client.DeleteAsync("/Trainer/me");
        var onIdentifier = await client.DeleteAsync($"/Trainer/{me.Id}");

        // 405 on `me`, where a GET is still routed, and 404 on the identifier, where nothing is
        // routed any more since the read by identifier was withdrawn. The two codes say the same
        // thing about deletion; only the surface around them differs.
        onMe.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        onIdentifier.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/Trainer/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -- Unauthorized --

    /// <summary>
    /// Get me, no token, returns 401.
    /// </summary>
    [Fact]
    public async Task GetMe_NoToken_Returns401()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/Trainer/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
