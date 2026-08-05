using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Controllers;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// Registration and sign-in over HTTP.
/// </summary>
/// <remarks>
/// Both hosts derive their auth controllers from the same <c>AuthControllerBase</c> and supply only
/// how the trainer is created — through an application service on one, a dispatched command on the
/// other. These assertions belong to both, and used to be two files that agreed on everything
/// except which two cases each had forgotten: duplicate-username was tested on one host and
/// duplicate-email on the other, for one method that checks both codes.
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class AuthTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource
{
    // -- Register --

    /// <summary>
    /// Register, valid data, returns 201 with the address of the trainer.
    /// </summary>
    [Fact]
    public async Task Register_ValidData_Returns201WithTheAddressOfTheTrainer()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();

        var response = await AuthHelper.RegisterAsync(client, request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var trainerId = await response.Content.ReadFromJsonAsync<Guid>();
        trainerId.Should().NotBeEmpty();

        // `/Trainer/me` rather than `/Trainer/{id}`, which is where this pointed until the read by
        // identifier was withdrawn for serving any trainer's profile to any caller. It is the only
        // address the created trainer has now; the identifier a caller needs is in the body, which
        // is why the address being relative to the token costs nothing here. See ADR 0011.
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.AbsolutePath.Should().Be("/Trainer/me");
    }

    /// <summary>
    /// Register, publishes a location that serves the trainer.
    /// </summary>
    [Fact]
    public async Task Register_PublishesALocationThatServesTheTrainer()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();

        var response = await AuthHelper.RegisterAsync(client, request);
        var location = response.Headers.Location!;

        // The address needs a token the registration deliberately does not hand out, so following
        // it means signing in first. Doing it here is what proves the header points at something
        // that exists — the base controller names that action by string, across assemblies, and a
        // rename would otherwise leave Location pointing nowhere with nothing to say so.
        var token = await AuthHelper.LoginAsync(client, request.Username, request.Password);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var followed = await client.GetAsync(location);

        followed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Register, also creates the trainer.
    /// </summary>
    [Fact]
    public async Task Register_AlsoCreatesTheTrainer()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var token = await AuthHelper.LoginAsync(client, request.Username, request.Password);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Registration creates the identity user and the trainer in one TransactionScope. If the
        // trainer half were mis-wired, the account would exist with no trainer behind it and this
        // would answer 404.
        var response = await client.GetAsync("/Trainer/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Register, without a bio, answers a null bio.
    /// </summary>
    [Fact]
    public async Task Register_WithoutABio_AnswersANullBio()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var token = await AuthHelper.LoginAsync(client, request.Username, request.Password);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Registration carries no bio, so the Bio column is null. This pins the round trip
        // through the database: an absent bio must come back as null, never as an empty
        // value object the mapping conjured out of a null column. See ADR 0032.
        var trainer = await client.GetFromJsonAsync<TrainerResponseHttp>("/Trainer/me");

        trainer!.Bio.Should().BeNull();
    }

    /// <summary>
    /// Register, duplicate email, returns 409.
    /// </summary>
    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var duplicate = AuthHelper.CreateUniqueRegisterRequest();
        var response = await AuthHelper.RegisterAsync(client, new RegisterRequest
        {
            Username = duplicate.Username,
            Email = request.Email,
            Password = duplicate.Password,
            ConfirmPassword = duplicate.ConfirmPassword,
            Firstname = "Dup",
            Lastname = "User"
        });

        // The request is well-formed; what it asks for is taken. Nothing the caller can fix by
        // re-reading it, which is the line between 400 and 409.
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Register, duplicate username, returns 409.
    /// </summary>
    [Fact]
    public async Task Register_DuplicateUsername_Returns409()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var duplicate = AuthHelper.CreateUniqueRegisterRequest();
        var response = await AuthHelper.RegisterAsync(client, new RegisterRequest
        {
            Username = request.Username,
            Email = duplicate.Email,
            Password = duplicate.Password,
            ConfirmPassword = duplicate.ConfirmPassword,
            Firstname = "Dup",
            Lastname = "User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Register, password mismatch, returns 400.
    /// </summary>
    [Fact]
    public async Task Register_PasswordMismatch_Returns400()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();

        var response = await AuthHelper.RegisterAsync(client, new RegisterRequest
        {
            Username = request.Username,
            Email = request.Email,
            Password = request.Password,
            ConfirmPassword = "something_else",
            Firstname = "Test",
            Lastname = "User"
        });

        // Malformed rather than taken: the caller can fix this by re-reading their own request,
        // which is exactly what separates it from the two above.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -- Login --

    /// <summary>
    /// Login, valid credentials, returns token.
    /// </summary>
    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/Auth/login", new LoginRequest
        {
            Username = request.Username,
            Password = request.Password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse!.Token.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Login, invalid password, returns 401.
    /// </summary>
    [Fact]
    public async Task Login_InvalidPassword_Returns401()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/Auth/login", new LoginRequest
        {
            Username = request.Username,
            Password = "wrong_password"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Login, unknown username, answers exactly like a wrong password.
    /// </summary>
    [Fact]
    public async Task Login_UnknownUsername_AnswersExactlyLikeAWrongPassword()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/Auth/login", new LoginRequest
        {
            Username = "nobody_by_that_name",
            Password = "whatever"
        });

        // Same status and same sentence as a wrong password, deliberately: telling the two apart
        // would make this endpoint an oracle for which accounts exist. The two call sites go
        // through one method so they cannot drift.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invalid username or password.");
    }
}
