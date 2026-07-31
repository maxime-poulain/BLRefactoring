using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// Registration and sign-in over HTTP, on the CQRS host.
/// </summary>
/// <remarks>
/// Both stacks derive from the same <c>AuthControllerBase</c>, but each supplies its own
/// trainer creation — this one through a command dispatched by Mediator. These tests prove
/// the CQRS wiring of that shared flow, not the flow itself.
/// </remarks>
[Collection("Api")]
public class AuthControllerTests(ApiFactory factory) : IntegrationTest(factory)
{
    // -- Register --

    [Fact]
    public async Task Register_ValidData_Returns200()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();

        var response = await AuthHelper.RegisterAsync(client, request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_AlsoCreatesTheTrainer()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var token = await AuthHelper.LoginAsync(client, request.Username, request.Password);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Registration dispatches CreateTrainerCommand inside the same TransactionScope as
        // the identity user. If the command side were mis-wired, the account would exist
        // with no trainer behind it and this would answer 404.
        var response = await client.GetAsync("/Trainer/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        await AuthHelper.RegisterAsync(client, request);

        var duplicate = AuthHelper.CreateUniqueRegisterRequest();
        duplicate = new RegisterRequest
        {
            Username = duplicate.Username,
            Email = request.Email,
            Password = "pass",
            ConfirmPassword = "pass",
            Firstname = "Dup",
            Lastname = "User"
        };

        var response = await AuthHelper.RegisterAsync(client, duplicate);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_PasswordMismatch_Returns400()
    {
        var client = Factory.CreateClient();
        var request = new RegisterRequest
        {
            Username = "mismatch_user",
            Email = "mismatch@example.com",
            Password = "pass",
            ConfirmPassword = "different",
            Firstname = "Test",
            Lastname = "User"
        };

        var response = await AuthHelper.RegisterAsync(client, request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -- Login --

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        await AuthHelper.RegisterAsync(client, request);

        var response = await client.PostAsJsonAsync("/Auth/login", new LoginRequest
        {
            Username = request.Username,
            Password = request.Password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_InvalidPassword_Returns401()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        await AuthHelper.RegisterAsync(client, request);

        var response = await client.PostAsJsonAsync("/Auth/login", new LoginRequest
        {
            Username = request.Username,
            Password = "wrong_password"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
