using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using BLRefactoring.Shared.Api.Contracts.Trainers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BLRefactoring.Api.TestKit;

/// <summary>
/// What the API answers when the body itself is wrong, before any command or service sees it.
/// </summary>
/// <remarks>
/// The request contracts declare their constraints as data annotations, so <c>[ApiController]</c>
/// rejects a malformed body at model binding with a <c>ValidationProblemDetails</c> keyed by field
/// name. That shape is the point: a form on the other end can mark each offending input instead of
/// showing one message for the whole submission.
/// <para>
/// Both stacks carry the same assertions on purpose. The contracts are one set of types shared by
/// the two hosts, so a request rejected by one must be rejected identically by the other — and a
/// test in a single suite would not notice the day that stopped being true. It used to be
/// duplicated rather than shared: the two files were identical to the byte.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class RequestValidationTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource
{
    [Fact]
    public async Task EveryInvalidField_IsReported_NotJustTheFirst()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var entityTag = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerRequestHttp
        {
            Firstname = "J",                    // shorter than the two characters a name needs
            Lastname = string.Empty,            // absent as far as [Required] is concerned
            ContactEmail = "someone@example.com"
        }, entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        // A caller filling a form deserves every invalid field at once rather than one round-trip
        // per mistake, and deserves to be told which ones.
        problem!.Errors.Should().ContainKeys(
            nameof(EditTrainerRequestHttp.Firstname),
            nameof(EditTrainerRequestHttp.Lastname));
    }

    [Fact]
    public async Task ValidBody_ReachesTheApplicationLayer()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var entityTag = await client.GetETagAsync("/Trainer/me");

        var response = await client.PutWithIfMatchAsync("/Trainer/me", new EditTrainerRequestHttp
        {
            Firstname = "Edited",
            Lastname = "Profile",
            ContactEmail = "edited.profile@example.com"
        }, entityTag);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
