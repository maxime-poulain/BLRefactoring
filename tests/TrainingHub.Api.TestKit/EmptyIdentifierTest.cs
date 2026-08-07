using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainings;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// What the API answers when a caller names the empty identifier.
/// </summary>
/// <remarks>
/// <c>Guid.Empty</c> is a well-formed <c>Guid</c> that names nothing, and it used to be answered
/// differently by the two hosts: 400 on the CQRS one, whose validation pipeline stopped it, and
/// 500 on the layered one, which has no pipeline and let it reach <c>EntityId.Create</c> — a
/// constructor that refuses the empty identifier by throwing. The gap was recorded twice before it
/// was closed, and this is the fact that closes it (ADR 0046).
/// <para>
/// Shared rather than duplicated, and for the reason that matters here more than anywhere: the
/// defect this replaces existed on exactly one host. A per-suite assertion would have passed on the
/// repository where the two disagreed, which is the state these tests exist to make impossible.
/// </para>
/// <para>
/// The body is a <c>ValidationProblemDetails</c> keyed by parameter name, because the refusal
/// happens at model binding like every other annotation on this boundary — not as a domain error
/// under <c>domainErrors</c>, which is what a business refusal looks like (ADR 0012).
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class EmptyIdentifierTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource
{
    /// <summary>
    /// Reading a training by the empty identifier, is refused, naming the parameter.
    /// </summary>
    [Fact]
    public async Task ReadingATrainingByTheEmptyIdentifier_IsRefused_NamingTheParameter()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.GetAsync($"/Training/{Guid.Empty}");

        await ShouldBeRefusedNaming(response, "trainingId");
    }

    /// <summary>
    /// Reading a photo by the empty identifier, is refused, naming the parameter.
    /// </summary>
    [Fact]
    public async Task ReadingAPhotoByTheEmptyIdentifier_IsRefused_NamingTheParameter()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.GetAsync($"/Trainer/{Guid.Empty}/photo");

        await ShouldBeRefusedNaming(response, "id");
    }

    /// <summary>
    /// Deleting a training by the empty identifier, is refused, naming the parameter.
    /// </summary>
    /// <remarks>
    /// A guarded route, and the reason this fact is worth stating three times rather than once:
    /// authorization runs before model binding, so the policy handler sees <c>Guid.Empty</c> first.
    /// It answers that no training bears that identifier, succeeds the requirement — existence is
    /// the action's concern, not the policy's — and the annotation gets its turn.
    /// </remarks>
    [Fact]
    public async Task DeletingATrainingByTheEmptyIdentifier_IsRefused_NamingTheParameter()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var response = await client.DeleteAsync($"/Training/{Guid.Empty}");

        await ShouldBeRefusedNaming(response, "trainingId");
    }

    /// <summary>
    /// Transferring to the empty recipient, is refused, naming the field.
    /// </summary>
    /// <remarks>
    /// The one identifier that travels in a body. It carried <c>[Required]</c> for as long as the
    /// contract existed, and <c>[Required]</c> refuses nothing on a non-nullable <c>Guid</c>: the
    /// value is always present. What refused it was a validator behind one host.
    /// </remarks>
    [Fact]
    public async Task TransferringToTheEmptyRecipient_IsRefused_NamingTheField()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var trainingId = await CreateTrainingAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/Training/{trainingId}/transfer",
            new TransferTrainingHttpRequest { RecipientTrainerId = Guid.Empty });

        await ShouldBeRefusedNaming(response, nameof(TransferTrainingHttpRequest.RecipientTrainerId));
    }

    private static async Task ShouldBeRefusedNaming(HttpResponseMessage response, string parameter)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the empty identifier names nothing, and a caller who sends one has made a malformed " +
            "request rather than found the server in a bad state");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        problem!.Errors.Keys.Should().Contain(
            key => string.Equals(key, parameter, StringComparison.OrdinalIgnoreCase),
            "a caller cannot fix what the answer does not name");
    }

    private static async Task<Guid> CreateTrainingAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/Training", TrainingRequests.Valid());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return await response.Content.ReadFromJsonAsync<Guid>();
    }
}
