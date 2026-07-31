using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Edit;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Controllers;

/// <summary>
/// The validation pipeline of the CQRS stack, seen from the outside.
/// </summary>
/// <remarks>
/// <para>
/// This is where the two stacks genuinely part ways. A bad field on the layered host is
/// rejected by the value objects and surfaces as the domain's own error collection. Here it
/// is rejected earlier, by a FluentValidation validator running inside
/// <c>ValidationPipelineBehavior</c>, which throws a <c>ValidationException</c> that
/// <c>FluentValidationMiddleware</c> turns into a 400 — with a completely different body.
/// </para>
/// <para>
/// Both halves of that chain only exist when a real request travels the ASP.NET Core
/// pipeline, which is why these assertions are made over HTTP rather than by dispatching a
/// command: the middleware would otherwise never run, and a unit test would see the raw
/// exception instead of the status code and payload a client actually receives.
/// </para>
/// </remarks>
[Collection("Api")]
public class ValidationPipelineTests(ApiFactory factory) : IntegrationTest(factory)
{
    private sealed record ValidationPayload(ValidationFailure[] Errors);

    private sealed record ValidationFailure(string PropertyName, string ErrorMessage);

    private static async Task<string[]> InvalidPropertiesAsync(HttpResponseMessage response)
    {
        // Read exactly the way a .NET client would. The middleware builds the payload with
        // WriteAsJsonAsync, which applies ASP.NET Core's web defaults and therefore emits
        // camelCase whatever the C# property names look like; ReadFromJsonAsync applies the
        // same defaults, which match case-insensitively. Parsing the document by hand and
        // asking for "Errors" reads the source rather than the wire, and misses that.
        var payload = await response.Content.ReadFromJsonAsync<ValidationPayload>();

        return [.. payload!.Errors.Select(failure => failure.PropertyName)];
    }

    [Fact]
    public async Task InvalidEmail_IsRejectedByTheValidator_NamingTheOffendingProperty()
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
        (await InvalidPropertiesAsync(response)).Should().Contain(nameof(EditTrainerCommand.ContactEmail));
    }

    [Fact]
    public async Task EveryBrokenRule_IsReported_NotJustTheFirst()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var entityTag = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerCommand
        {
            Firstname = string.Empty,
            Lastname = string.Empty,
            ContactEmail = "not-an-email"
        }, entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A caller filling a form deserves every invalid field at once rather than one
        // round-trip per mistake.
        (await InvalidPropertiesAsync(response)).Should().Contain(
        [
            nameof(EditTrainerCommand.Firstname),
            nameof(EditTrainerCommand.Lastname),
            nameof(EditTrainerCommand.ContactEmail)
        ]);
    }

    [Fact]
    public async Task ValidationRunsBeforeTheHandler_LeavingTheAggregateUntouched()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var before = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerCommand
        {
            Firstname = "Rejected",
            Lastname = "Edit",
            ContactEmail = "not-an-email"
        }, before);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The behaviour throws before calling the handler, so nothing was saved and the
        // version the caller holds is still current — their next edit must not need a reload.
        var after = await client.GetETagAsync("/Trainer/me");
        after.Should().Be(before);
    }
}
