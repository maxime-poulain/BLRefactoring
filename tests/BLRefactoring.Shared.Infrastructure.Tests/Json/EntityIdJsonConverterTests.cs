using System.Text.Json;
using AwesomeAssertions;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Infrastructure.Outbox;
using Xunit;

namespace BLRefactoring.Shared.Infrastructure.Tests.Json;

/// <summary>
/// How strongly-typed identifiers cross into an outbox payload and back.
/// </summary>
/// <remarks>
/// Uses the outbox's own <see cref="OutboxSerializer.Options"/> rather than a hand-built set, so
/// these assert the configuration messages are actually written with. A converter that works in
/// isolation but is not registered would be worse than none.
/// </remarks>
public class EntityIdJsonConverterTests
{
    private sealed record Payload(TrainerId TrainerId, TrainingId TrainingId);

    [Fact]
    public void Identifiers_AreWrittenAsBareValues()
    {
        var payload = new Payload(
            TrainerId.Create(Guid.Parse("d4b6b8d2-3d69-4a9e-b2e8-3c5b4b5b9f6e")),
            TrainingId.Create(Guid.Parse("8f8d7d71-cbb8-49b5-b5d8-1fd3d20a4c27")));

        var json = JsonSerializer.Serialize(payload, OutboxSerializer.Options);

        // The shape is the contract. A wrapper object would be valid JSON and utterly unusable
        // by anything that is not this codebase.
        json.Should().Be(
            """
            {"trainerId":"d4b6b8d2-3d69-4a9e-b2e8-3c5b4b5b9f6e","trainingId":"8f8d7d71-cbb8-49b5-b5d8-1fd3d20a4c27"}
            """);
    }

    [Fact]
    public void Identifiers_SurviveTheRoundTrip_AsTheirDerivedType()
    {
        var original = new Payload(TrainerId.Generate(), TrainingId.Generate());

        var restored = JsonSerializer.Deserialize<Payload>(
            JsonSerializer.Serialize(original, OutboxSerializer.Options), OutboxSerializer.Options);

        restored.Should().Be(original);
        restored!.TrainerId.Should().BeOfType<TrainerId>();
        restored.TrainingId.Should().BeOfType<TrainingId>();
    }

    [Fact]
    public void EmptyIdentifier_IsRejectedOnTheWayBackIn()
    {
        var act = () => JsonSerializer.Deserialize<TrainerId>(
            $"\"{Guid.Empty}\"", OutboxSerializer.Options);

        // Reading goes through Create, so a stored value the domain considers impossible cannot
        // be materialised by the serialiser either. Without this, deserialisation would be a
        // hole straight through the invariant.
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UnrelatedTypes_AreLeftAlone()
    {
        // The factory matches on the EntityId base, not on anything that merely looks like an
        // identifier, so ordinary values keep their normal representation.
        JsonSerializer.Serialize(new { Value = 42 }, OutboxSerializer.Options)
            .Should().Be("""{"value":42}""");
    }
}
