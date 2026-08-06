using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Contracts.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// One error shape, wherever a request fails.
/// </summary>
/// <remarks>
/// This API used to answer with four different bodies depending on how far a request got: a bare
/// array for a business failure, a <c>ValidationProblemDetails</c> for a data annotation, an
/// invented <c>{ errors: [{ propertyName, errorMessage }] }</c> for a FluentValidation failure, and
/// — for anything unhandled — a bare JSON string, quotes and all. A client had to parse four
/// shapes for one API, with nothing telling it which to expect.
/// <para>
/// Run against both hosts, which is what ADR 0004 promises and what this file could not deliver
/// while it existed on one suite only: the handlers under test live in <c>Shared.Api</c>, so a
/// regression on the untested host would have been invisible.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class ErrorFormatTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource
{
    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    /// <summary>
    /// Business failure, is a problem document carrying the domain code.
    /// </summary>
    [Fact]
    public async Task BusinessFailure_IsAProblemDocumentCarryingTheDomainCode()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        (await client.PostAsJsonAsync("/Training", TrainingRequests.Valid("A duplicated title")))
            .EnsureSuccessStatusCode();
        var response = await client.PostAsJsonAsync("/Training", TrainingRequests.Valid("A duplicated title"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // The media type is part of the shape. Business failures used to answer application/json
        // while every framework-produced error answered application/problem+json, so "one shape"
        // was true of the body and false of the header describing it.
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await BodyAsync(response);
        body.ValueKind.Should().Be(JsonValueKind.Object, "an error body is a document, not a bare string");
        body.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status409Conflict);

        // The domain code survives the move to RFC 7807 — a client branching on it keeps its logic
        // and changes only where it reads it from. Under `domainErrors`, not `errors`: that name
        // belongs to the field map a ValidationProblemDetails publishes, and the two shapes were
        // colliding under it.
        //
        // The code is a string, and it names its owner. It used to be a nested { name, value } pair
        // because the kernel's smart enum happened to hold a number; nothing ever read the number.
        body.GetProperty("domainErrors")
            .EnumerateArray()
            .Select(error => error.GetProperty("errorCode").GetString())
            .Should().Contain("Training.DuplicateTitle");
    }

    /// <summary>
    /// Malformed email, is refused by the domain, with the same code on both hosts.
    /// </summary>
    /// <remarks>
    /// ADR 0043's central claim, and the one thing about it a rule cannot see. The CQRS validator
    /// used to carry <c>EmailAddress()</c> and the layered stack never did, so the same request was
    /// refused one layer apart and answered two different codes depending on which host received
    /// it — a divergence ADR 0016 measured, named, and recorded as a cost it was not paying.
    /// <para>
    /// It is asserted here rather than in either suite because that is the whole of the claim: a
    /// per-host test would pass on a repository where the two hosts had drifted apart again, which
    /// is exactly the state this replaces. The address is refused by <c>Email.Create</c> now, on
    /// both, and the code a client branches on says which aggregate refused it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task MalformedEmail_IsRefusedByTheDomain_WithTheSameCodeOnBothHosts()
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
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await BodyAsync(response);

        // Not under `errors`: the contract deliberately declares no shape for an address — .NET's
        // [EmailAddress] and the domain's validator disagree in both directions — so this refusal
        // is a business failure and reads like one.
        body.TryGetProperty("errors", out _)
            .Should().BeFalse("the address is judged by the domain, not by a data annotation");

        body.GetProperty("domainErrors")
            .EnumerateArray()
            .Select(error => error.GetProperty("errorCode").GetString())
            .Should().Contain("Trainer.InvalidEmail");
    }

    /// <summary>
    /// Validation failure, keeps errors for the field map.
    /// </summary>
    [Fact]
    public async Task ValidationFailure_KeepsErrorsForTheFieldMap()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var entityTag = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerRequestHttp
        {
            Firstname = "J",
            Lastname = string.Empty,
            ContactEmail = "someone@example.com"
        }, entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await BodyAsync(response);

        // The other half of the rename: `errors` means one thing now, and it is this one — the map
        // RFC 7807 defines, keyed by field. It used to mean this on one host and an array of domain
        // codes on the other, for the same request.
        body.GetProperty("errors").ValueKind.Should().Be(JsonValueKind.Object);
        body.GetProperty("errors").TryGetProperty(nameof(EditTrainerRequestHttp.Firstname), out _)
            .Should().BeTrue();
    }

    /// <summary>
    /// Missing if match, is the same shape.
    /// </summary>
    [Fact]
    public async Task MissingIfMatch_IsTheSameShape()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        // No If-Match: refused before the application layer hears about it, and answered by the
        // API on its own — the one failure with no Result behind it.
        var response = await client.PutAsJsonAsync("/Trainer/me", new EditTrainerRequestHttp
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "edited@example.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);

        var body = await BodyAsync(response);
        body.ValueKind.Should().Be(JsonValueKind.Object);
        body.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status428PreconditionRequired);

        // Not TryGetProperty: that passes on `domainErrors: null`, on `[]`, and on `3`.
        body.GetProperty("domainErrors")
            .EnumerateArray()
            .Should().NotBeEmpty("the failure carries a code and the caller is entitled to read it");
    }

    /// <summary>
    /// Malformed body, is rejected at model binding, as a problem document.
    /// </summary>
    [Fact]
    public async Task MalformedBody_IsRejectedAtModelBinding_AsAProblemDocument()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.PostAsync(
            "/Training",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        // A hard 400, not BeOneOf(BadRequest, InternalServerError). The body cannot be bound, so
        // [ApiController] rejects it before any handler runs — the unhandled-exception handler is
        // never entered, and asserting "one of these two" let this test pass without ever deciding
        // which. That handler is covered directly, in UnhandledExceptionHandlerTests.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await BodyAsync(response);
        body.ValueKind.Should().Be(JsonValueKind.Object);
        body.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status400BadRequest);

        // Whatever went wrong, the caller is not told where in the server it went wrong.
        body.GetRawText().Should().NotContain("StackTrace").And.NotContain("TrainingHub.");
    }

    /// <summary>
    /// Sign in failure, is a problem document.
    /// </summary>
    [Fact]
    public async Task SignInFailure_IsAProblemDocument()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/Auth/login", new LoginRequestHttp
        {
            Username = request.Username,
            Password = "wrong_password"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The endpoint every client meets first, and the last one still answering a bare JSON
        // string — the shape ADR 0004 calls the worst of the four it removed.
        var body = await BodyAsync(response);
        body.ValueKind.Should().Be(JsonValueKind.Object, "a 401 is an error like any other");
        body.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status401Unauthorized);
        body.GetProperty("detail").GetString().Should().Be("Invalid username or password.");
    }

    /// <summary>
    /// Registration failure, is a problem document keyed by field.
    /// </summary>
    [Fact]
    public async Task RegistrationFailure_IsAProblemDocumentKeyedByField()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var duplicate = AuthHelper.CreateUniqueRegisterRequest();
        var response = await AuthHelper.RegisterAsync(client, new RegisterRequestHttp
        {
            Username = duplicate.Username,
            Email = request.Email,
            Password = duplicate.Password,
            ConfirmPassword = duplicate.ConfirmPassword,
            Firstname = "Dup",
            Lastname = "User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await BodyAsync(response);

        // Identity's codes are names with no numeric value, so they are published as the field they
        // are about rather than forced into this API's own code enum. It used to be a bare
        // IdentityError array — the shape ADR 0004 removed from forty other places.
        body.GetProperty("errors")
            .TryGetProperty(nameof(RegisterRequestHttp.Email), out var emailMessages)
            .Should().BeTrue("the caller has to know which field is taken");
        emailMessages.EnumerateArray().Should().NotBeEmpty();
    }
}
