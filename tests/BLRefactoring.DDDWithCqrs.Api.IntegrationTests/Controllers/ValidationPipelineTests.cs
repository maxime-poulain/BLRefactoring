using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures;
using BLRefactoring.Shared.Api.Contracts.Trainers;
using Microsoft.AspNetCore.Mvc;
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
/// <c>ValidationExceptionHandler</c> turns into a 400.
/// </para>
/// <para>
/// The body is a <c>ValidationProblemDetails</c> keyed by field name — the same shape a data
/// annotation failure produces, which is the whole point of routing it through that handler:
/// where a request failed should not change how a client reads why.
/// </para>
/// <para>
/// Both halves of that chain only exist when a real request travels the ASP.NET Core
/// pipeline, which is why these assertions are made over HTTP rather than by dispatching a
/// command: the handler would otherwise never run, and a unit test would see the raw
/// exception instead of the status code and payload a client actually receives.
/// </para>
/// </remarks>
[Collection("Api")]
public class ValidationPipelineTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static async Task<string[]> InvalidPropertiesAsync(HttpResponseMessage response)
    {
        // Deserialised into the framework's own type, so the test asserts the published contract
        // rather than a local restatement of it: if the shape stops being a ValidationProblemDetails,
        // this stops compiling against reality instead of quietly reading nothing.
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        return [.. problem!.Errors.Keys];
    }

    [Fact]
    public async Task InvalidEmail_IsRejectedByTheValidator_NamingTheOffendingProperty()
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
        (await InvalidPropertiesAsync(response)).Should().Contain(nameof(EditTrainerRequestHttp.ContactEmail));
    }

    [Fact]
    public async Task ValidationRunsBeforeTheHandler_LeavingTheAggregateUntouched()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var before = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerRequestHttp
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
