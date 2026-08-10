using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Contracts.Trainings;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The transfer rule over HTTP, on both hosts: a training changes hands when the recipient's
/// catalog allows it, and the transferred fact rides the outbox (ADR 0036).
/// </summary>
/// <remarks>
/// The decision itself is <c>TrainingTransferDomainService</c>'s, proven in the domain suite against
/// mocked ports. What only this suite can prove is the whole chain: the policy admitting the
/// giver, the application layers of both stacks orchestrating the same decision, ownership
/// actually moving in the database, and the fact committing into the outbox for the index.
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class TrainingTransferTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IServiceScopeSource, IHttpClientSource
{
    /// <summary>
    /// A transferred training, changes hands, and commits the transferred fact.
    /// </summary>
    [Fact]
    public async Task ATransferredTraining_ChangesHands_AndCommitsTheTransferredFact()
    {
        var giver = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var created = await giver.PostAsJsonAsync("/Training", TrainingRequests.Valid("A training to hand over"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var trainingId = await created.Content.ReadFromJsonAsync<Guid>();

        var recipient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var recipientId = (await recipient.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!.Id;

        var transferred = await giver.PostAsJsonAsync(
            $"/Training/{trainingId}/transfer",
            new TransferTrainingHttpRequest { RecipientTrainerId = recipientId });
        transferred.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The giver can no longer read what they gave away; the recipient's list gained it.
        (await giver.GetAsync($"/Training/{trainingId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var mine = await recipient.GetFromJsonAsync<JsonElement>("/Training/my-trainings?page=1&pageSize=10");
        mine.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should().Contain(trainingId);

        // The fact rode the outbox: committed with the transfer, carrying both owners.
        var fact = await WaitForTransferredFactAsync();
        var payload = JsonDocument.Parse(fact.Payload).RootElement;
        payload.GetProperty("TrainingId").GetGuid().Should().Be(trainingId);
        payload.GetProperty("NewTrainerId").GetGuid().Should().Be(recipientId);
    }

    /// <summary>
    /// A recipient with the same title, refuses the transfer.
    /// </summary>
    [Fact]
    public async Task ARecipientWithTheSameTitle_RefusesTheTransfer()
    {
        var giver = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var created = await giver.PostAsJsonAsync("/Training", TrainingRequests.Valid("A contested title"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var trainingId = await created.Content.ReadFromJsonAsync<Guid>();

        var recipient = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);
        var recipientId = (await recipient.GetFromJsonAsync<TrainerHttpResponse>("/Trainer/me"))!.Id;
        (await recipient.PostAsJsonAsync("/Training", TrainingRequests.Valid("A contested title")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var refused = await giver.PostAsJsonAsync(
            $"/Training/{trainingId}/transfer",
            new TransferTrainingHttpRequest { RecipientTrainerId = recipientId });

        // The same business sentence creation and edit refuse with, as a problem document whose
        // code a caller can branch on (ADR 0015, ADR 0016).
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        refused.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("domainErrors")
            .EnumerateArray()
            .Select(error => error.GetProperty("errorCode").GetString())
            .Should().Contain(TrainingErrorCodes.DuplicateTitle.Value);
    }

    /// <summary>
    /// Polls the outbox until the transferred fact is delivered, and answers its envelope. The
    /// polling shape and its reasons are <c>OutboxTest.WaitForMessageAsync</c>'s.
    /// </summary>
    private async Task<OutboxMessage> WaitForTransferredFactAsync()
    {
        var timeout = TimeSpan.FromSeconds(15);
        var started = DateTime.UtcNow;
        OutboxMessage? lastSeen = null;

        while (DateTime.UtcNow - started < timeout)
        {
            using (var scope = Factory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();
                lastSeen = (await context.Set<OutboxMessage>().ToListAsync())
                    .SingleOrDefault(message => message.Name == "TrainingTransferred");

                if (lastSeen is { ProcessedOnUtc: not null })
                {
                    return lastSeen;
                }
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException(
            $"The transferred fact was not delivered within {timeout.TotalSeconds}s. " +
            (lastSeen is null ? "No such message exists." : $"Last seen: Attempts={lastSeen.Attempts}, Error={lastSeen.Error ?? "null"}."));
    }
}
