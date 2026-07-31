using System.Net.Http.Json;
using AwesomeAssertions;
using BLRefactoring.DDD.Api.IntegrationTests.Fixtures;
using BLRefactoring.Shared.Application.IntegrationEvents;
using BLRefactoring.Shared.Infrastructure.Outbox;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BLRefactoring.DDD.Api.IntegrationTests.Controllers;

/// <summary>
/// The transactional outbox, observed end to end.
/// </summary>
/// <remarks>
/// The defect this replaced is easy to state and easy to reintroduce: the welcome email was sent
/// from a domain event handler, which runs inside <c>SavingChangesAsync</c>, so it left before
/// the commit. A transaction failing afterwards left a welcome message delivered for a trainer
/// that never existed. These assert the property that makes it impossible — the announcement is
/// a row written by the same transaction as the fact, and nothing leaves during the request.
/// </remarks>
[Collection("Api")]
public class OutboxTests(ApiFactory factory) : IntegrationTest(factory)
{
    private async Task<List<OutboxMessage>> PendingMessagesAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();

        return await context.Set<OutboxMessage>()
            .AsNoTracking()
            .OrderBy(message => message.OccurredOn)
            .ToListAsync();
    }

    [Fact]
    public async Task Registering_WritesTheAnnouncementInTheSameTransaction()
    {
        var client = Factory.CreateClient();
        var request = AuthHelper.CreateUniqueRegisterRequest();

        (await AuthHelper.RegisterAsync(client, request)).EnsureSuccessStatusCode();

        var pending = await PendingMessagesAsync();

        var registration = pending.Should()
            .ContainSingle(message => message.Type.Contains(nameof(TrainerRegisteredIntegrationEvent)))
            .Subject;

        // Still pending: the request committed the trainer and queued the announcement, and
        // publication is somebody else's job, later.
        registration.ProcessedOn.Should().BeNull();
        registration.AttemptCount.Should().Be(0);
        registration.Error.Should().BeNull();
    }

    [Fact]
    public async Task TheStoredPayload_CarriesIdentifiersFlat_AndValueObjectsAsPrimitives()
    {
        var client = Factory.CreateClient();
        (await AuthHelper.RegisterAsync(client, AuthHelper.CreateUniqueRegisterRequest()))
            .EnsureSuccessStatusCode();

        var payload = (await PendingMessagesAsync())
            .Single(message => message.Type.Contains(nameof(TrainerRegisteredIntegrationEvent)))
            .Payload;

        // This is the contract a future deployment reads back, so its shape is asserted on the
        // stored text rather than on a round-tripped object: an identifier is a bare string, not
        // a wrapper, and the name arrives already flattened.
        payload.Should().Contain("\"trainerId\":\"").And.NotContain("\"value\"");
        payload.Should().Contain("\"firstname\":").And.Contain("\"contactEmail\":");
    }

    [Fact]
    public async Task CreatingATraining_QueuesItsPublication()
    {
        var client = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        var creation = await client.PostAsJsonAsync("/Training", new
        {
            Title = "An Outbox Training",
            Description = "A valid training description for integration testing",
            Prerequisites = "Basic programming knowledge required",
            AcquiredSkills = "Advanced design patterns mastery",
            Topics = new[] { "Programming" }
        });
        creation.EnsureSuccessStatusCode();

        (await PendingMessagesAsync())
            .Should().Contain(message => message.Type.Contains(nameof(TrainingPublishedIntegrationEvent)));
    }

    [Fact]
    public async Task TheProcessor_PublishesPendingMessages_AndMarksThemOnce()
    {
        var client = Factory.CreateClient();
        (await AuthHelper.RegisterAsync(client, AuthHelper.CreateUniqueRegisterRequest()))
            .EnsureSuccessStatusCode();

        // Built rather than resolved: the fixture removes the hosted service so the queue only
        // moves when a test says so, and constructing it here says exactly that.
        var processor = new OutboxProcessor(
            Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Factory.Services.GetRequiredService<ILogger<OutboxProcessor>>(),
            Factory.Services.GetRequiredService<TimeProvider>());

        await processor.ProcessPendingAsync(CancellationToken.None);

        var processed = await PendingMessagesAsync();
        processed.Should().NotBeEmpty();
        processed.Should().AllSatisfy(message =>
        {
            message.ProcessedOn.Should().NotBeNull();
            message.Error.Should().BeNull();
            message.AttemptCount.Should().Be(1);
        });

        // Draining again must not republish: the marking is what makes a second pass a no-op,
        // and without it the poller would resend every message on every tick.
        await processor.ProcessPendingAsync(CancellationToken.None);

        (await PendingMessagesAsync()).Should().AllSatisfy(
            message => message.AttemptCount.Should().Be(1));
    }
}
