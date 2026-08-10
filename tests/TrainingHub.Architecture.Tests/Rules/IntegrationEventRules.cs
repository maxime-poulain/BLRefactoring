using System.Reflection;
using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Outbox;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What may leave the bounded context, and in what shape.
/// </summary>
/// <remarks>
/// Integration events are the published half of ADR 0002's split: facts serialized into the outbox
/// and read back later, possibly by another process, possibly by a version of this code that no
/// longer exists. Everything these rules pin — sealedness, primitive payloads, freedom from the
/// in-process messaging library, a registered wire name — is a property a stored message depends on
/// long after the commit that wrote it — and each is consumed by the worker's contract, never the
/// in-process bus. See ADR 0024 and ADR 0025.
/// </remarks>
public sealed partial class IntegrationEventRules
{
    private static IEnumerable<Type> IntegrationEvents =>
        Solution.Application.DeclaredTypes()
            .Where(type => typeof(IIntegrationEvent).IsAssignableFrom(type)
                           && !type.IsInterface
                           && !type.IsAbstract);

    /// <summary>
    /// Every integration event, is a sealed record.
    /// </summary>
    [Fact]
    [ArchitectureRule("0024",
        "an integration event is a sealed, immutable record of a fact that already happened")]
    public void EveryIntegrationEvent_IsASealedRecord() =>
        IntegrationEvents
            .Selected("integration event")
            .Where(integrationEvent => !integrationEvent.IsSealed || !IsRecord(integrationEvent))
            .Select(integrationEvent => $"{integrationEvent.Name} is not a sealed record")
            .ShouldHold();

    /// <summary>
    /// Every integration event, carries only primitives.
    /// </summary>
    /// <remarks>
    /// The deliberate mirror of <c>EveryDomainEvent_CarriesOnlyDomainTypes</c>. A domain event
    /// speaks value objects because its handlers share the model; an integration event speaks
    /// primitives because its consumers must not have to.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0024",
        "the payload is primitives only, so a consumer deserializes the fact without the domain model")]
    public void EveryIntegrationEvent_CarriesOnlyPrimitives() =>
        IntegrationEvents
            .SelectMany(integrationEvent => integrationEvent
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => (integrationEvent, property)))
            .Selected("property on an integration event")
            .Where(pair => !IsPrimitiveEnough(pair.property.PropertyType))
            .Select(pair =>
                $"{pair.integrationEvent.Name}.{pair.property.Name} is a {pair.property.PropertyType.Name}. " +
                "A domain type on the wire forces every consumer to reference the domain")
            .ShouldHold();

    /// <summary>
    /// No integration event, touches the messaging library.
    /// </summary>
    /// <remarks>
    /// Checked over the full interface closure rather than the declared list: for a serialized
    /// message even transitive coupling is coupling, and the closure is also what catches an
    /// integration event quietly inheriting <c>IDomainEvent</c> — whose own interfaces reach
    /// <c>Mediator.INotification</c>. Keeping the marker clean is what keeps
    /// <c>IMediator.Publish(integrationEvent)</c> from compiling: the pre-commit bus and the
    /// post-commit outbox stay two different things the type system can tell apart.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0002",
        "integration events must not inherit INotification: tolerable for an in-process message and wrong " +
        "for one that is serialized and read back by another process")]
    public void NoIntegrationEvent_TouchesTheMessagingLibrary() =>
        IntegrationEvents
            .Append(typeof(IIntegrationEvent))
            .Selected("integration event, and the marker itself")
            .Where(integrationEvent => integrationEvent.GetInterfaces()
                .Any(contract => contract.Namespace?.StartsWith("Mediator", StringComparison.Ordinal) == true))
            .Select(integrationEvent =>
                $"{integrationEvent.Name} reaches a Mediator interface. A message that outlives the " +
                "process must not carry the process's own bus contract")
            .ShouldHold();

    /// <summary>
    /// Every integration event, has a stable name.
    /// </summary>
    /// <remarks>
    /// Both directions, like the kernel's recorded-types rule: an event missing from the registry
    /// would serialize nothing and fail at runtime instead of at build time, and a registry entry
    /// pointing at something that is not an integration event is a wire name promising a payload
    /// that cannot exist.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0024",
        "every integration event travels under a stable name and version declared in one explicit registry")]
    public void EveryIntegrationEvent_HasAStableName()
    {
        var registered = IntegrationEventTypes.All;

        IntegrationEvents
            .Selected("integration event")
            .Where(integrationEvent => !registered.Contains(integrationEvent))
            .Select(integrationEvent =>
                $"{integrationEvent.Name} has no entry in {nameof(IntegrationEventTypes)}, so it has no " +
                "wire name and the publisher will refuse it at runtime")
            .Concat(registered
                .Where(entry => !typeof(IIntegrationEvent).IsAssignableFrom(entry))
                .Select(entry =>
                    $"{entry.Name} is registered as an integration event and is not one. The registry " +
                    "names what crosses the boundary, nothing else"))
            .ShouldHold();
    }

    /// <summary>
    /// Every integration event, appears in the strategic design.
    /// </summary>
    /// <remarks>
    /// The mirror of <c>EveryDomainEvent_AppearsInTheEventStorming</c>, one boundary further out:
    /// an integration event is precisely the kind of fact the context map's consumers subscribe
    /// to, so one the documents never mention is a published contract the strategic design does
    /// not know it has.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0023",
        "document the strategic design, and hold it to the model")]
    public void EveryIntegrationEvent_AppearsInTheStrategicDesign()
    {
        var boards = SourceTree.ReadText(
            Path.Combine(SourceTree.RepositoryRoot, "docs", "strategic-design", "event-storming.md"));

        IntegrationEvents
            .Selected("integration event")
            .Where(integrationEvent => !boards.Contains(integrationEvent.Name, StringComparison.Ordinal))
            .Select(integrationEvent =>
                $"{integrationEvent.Name} crosses the bounded context and is named nowhere in " +
                "docs/strategic-design/event-storming.md. A published fact belongs on the board")
            .ShouldHold();
    }

    /// <summary>
    /// Every integration event, is consumed.
    /// </summary>
    /// <remarks>
    /// The mirror of <c>EveryDomainEvent_HasAHandler</c>, one boundary further out: the worker
    /// delivers whatever the registry names, and a fact with no consumer would be claimed,
    /// dispatched to nobody, and marked processed — silently, forever.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0025",
        "a fact nobody consumes is a decision that was never finished")]
    public void EveryIntegrationEvent_IsConsumed()
    {
        var consumed = Solution.Application.DeclaredTypes()
            .SelectMany(consumer => consumer.GetInterfaces())
            .Where(contract => contract.IsGenericType
                               && contract.GetGenericTypeDefinition().Name.StartsWith("IIntegrationEventHandler", StringComparison.Ordinal))
            .Select(contract => contract.GetGenericArguments()[0])
            .ToHashSet();

        IntegrationEvents
            .Selected("integration event")
            .Where(integrationEvent => !consumed.Contains(integrationEvent))
            .Select(integrationEvent =>
                $"nothing consumes {integrationEvent.Name}. The worker would deliver it to nobody and " +
                "mark it processed anyway")
            .ShouldHold();
    }

    /// <summary>
    /// No integration event handler, commits.
    /// </summary>
    [Fact]
    [ArchitectureRule("0025",
        "a consumer runs after the commit it reacts to, so one that writes through the unit of work " +
        "opens a transaction nobody scoped")]
    public void NoIntegrationEventHandler_Commits() =>
        Solution.Application.DeclaredTypes()
            .Where(consumer => consumer.GetInterfaces().Any(contract =>
                contract.IsGenericType
                && contract.GetGenericTypeDefinition().Name.StartsWith("IIntegrationEventHandler", StringComparison.Ordinal)))
            .Selected("integration event handler")
            .SelectMany(consumer => consumer.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.ParameterType.Name is "IUnitOfWork" or "TrainingContext")
                .Select(parameter =>
                    $"{consumer.Name} takes {parameter.ParameterType.Name}. It runs after the commit; " +
                    "what it needs to change belongs to a command, not a consumer"))
            .ShouldHold();

    /// <summary>
    /// No integration event consumer, reads through a repository.
    /// </summary>
    /// <remarks>
    /// One interface further out than its sibling above, and for the same reason. That rule refuses
    /// <c>IUnitOfWork</c> and <c>TrainingContext</c>, which stops a consumer committing — and stops
    /// nothing at all about <c>ITrainerRepository</c>, whose surface is <c>Add</c>, <c>Update</c>,
    /// <c>Delete</c> and a handful of reads. Handing that to something that runs after the commit
    /// hands it every write it was just forbidden, by another name.
    /// <para>
    /// So a consumer that needs a fact about the write model asks a port that cannot write. Since
    /// ADR 0056 that is not hypothetical: three consumers look up where a trainer is reachable, and
    /// the shape they use is the shape this rule keeps them to.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0056",
        "a post-commit consumer reads through a port that cannot write, never through a repository")]
    public void NoIntegrationEventConsumer_ReadsThroughARepository() =>
        Solution.Application.DeclaredTypes()
            .Where(consumer => consumer.GetInterfaces().Any(contract =>
                contract.IsGenericType
                && contract.GetGenericTypeDefinition().Name.StartsWith("IIntegrationEventHandler", StringComparison.Ordinal)))
            .Selected("integration event handler")
            .SelectMany(consumer => consumer.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.ParameterType.Name.EndsWith("Repository", StringComparison.Ordinal))
                .Select(parameter =>
                    $"{consumer.Name} takes {parameter.ParameterType.Name}. A repository can write, and a " +
                    "consumer may not; what it needs to read belongs behind a port that answers the " +
                    "question and nothing else"))
            .ShouldHold();

    /// <summary>
    /// Every integration event consumer, owns a stable name.
    /// </summary>
    /// <remarks>
    /// Two halves. The source half scans every implementation in the repository — test doubles
    /// included — for a <c>ConsumerName</c> written as a string literal: <c>nameof</c> or
    /// <c>GetType()</c> would tie the delivery ledger to a type name a refactoring could change,
    /// the exact mistake the wire-name registry exists to prevent (ADR 0024). The reflection half
    /// reads the production names and holds them usable: non-empty, within the 128 the column
    /// stores, and unique among the consumers of one event — two consumers sharing a name would
    /// share one ledger row and one of them would never run.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0034",
        "a consumer's ledger identity is a hand-written stable name, never a type name a refactoring could change")]
    public void EveryIntegrationEventConsumer_OwnsAStableName()
    {
        var named = Solution.Application.DeclaredTypes()
            .SelectMany(consumer => consumer.GetInterfaces()
                .Where(contract => contract.IsGenericType
                                   && contract.GetGenericTypeDefinition().Name.StartsWith("IIntegrationEventHandler", StringComparison.Ordinal))
                .Select(contract => (Consumer: consumer, Event: contract.GetGenericArguments()[0])))
            .Select(pair => (pair.Consumer, pair.Event, Name: ConsumerNameOf(pair.Consumer)))
            .ToArray();

        SourceTree.SourceFiles
            .Where(path => !SourceTree.IsGenerated(path))
            .Select(path => (Path: SourceTree.Relative(path), Text: SourceTree.ReadText(path)))
            .Where(file => ConsumerImplementation.IsMatch(file.Text))
            .Selected("integration event consumer implementation")
            .Where(file => !LiteralConsumerName.IsMatch(file.Text)
                           || DerivedConsumerName.IsMatch(file.Text))
            .Select(file =>
                $"'{file.Path}' implements a consumer whose ConsumerName is not a hand-written string " +
                "literal. A name derived from the type changes the day the type is renamed, and the " +
                "ledger would forget every delivery recorded under the old one")
            .Concat(named
                .Where(pair => pair.Name is null)
                .Select(pair =>
                    $"{pair.Consumer.Name} declares no ConsumerName, so the ledger cannot record its deliveries"))
            .Concat(named
                .Where(pair => pair.Name is not null && (pair.Name.Length == 0 || pair.Name.Length > 128))
                .Select(pair =>
                    $"{pair.Consumer.Name} declares '{pair.Name}', which does not fit the ledger's " +
                    "128-character ConsumerName column"))
            .Concat(named
                .Where(pair => pair.Name is not null)
                .GroupBy(pair => (pair.Event, pair.Name))
                .Where(group => group.Count() > 1)
                .Select(group =>
                    $"{string.Join(" and ", group.Select(pair => pair.Consumer.Name))} both consume " +
                    $"{group.Key.Event.Name} under the name '{group.Key.Name}'. They would share one " +
                    "ledger row, and one of them would never run"))
            .ShouldHold();
    }

    /// <summary>
    /// The declared consumer name, read off an instance built with null dependencies — safe
    /// because the rule above requires the getter to be a literal, which touches nothing.
    /// Answers <see langword="null"/> when the consumer declares no such property.
    /// </summary>
    private static string? ConsumerNameOf(Type consumer)
    {
        var property = consumer.GetProperty("ConsumerName");

        if (property is null)
        {
            return null;
        }

        var constructor = consumer.GetConstructors().Single();
        var instance = constructor.Invoke(new object?[constructor.GetParameters().Length]);

        return (string?)property.GetValue(instance);
    }

    /// <summary>A file declaring an implementation of the consumer contract.</summary>
    [GeneratedRegex(@":\s*IIntegrationEventHandler<")]
    private static partial Regex ConsumerImplementation { get; }

    /// <summary>
    /// The operator's surface, offers no way to forget a message.
    /// </summary>
    /// <remarks>
    /// The port is held to exactly two operations because the third is the dangerous one. A poison
    /// row is the evidence of a fact the system committed and then failed to announce; the sweep
    /// already refuses to touch it — <em>"deleting it would be the mechanism destroying its own
    /// crime scene"</em> — and a surface that offered a discard would hand that same deletion to a
    /// person, one click away from the listing that shows how noisy the backlog is.
    /// <para>
    /// A closed set rather than a name check, on the mold of <c>IObjectStore</c>'s: what matters is
    /// not that nothing is called <c>Delete</c> but that nothing else is offered at all.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0061",
        "the operator may retry a poison message or leave it, and nothing offers to forget one")]
    public void TheOperatorsSurface_OffersNoWayToForgetAMessage()
    {
        string[] expected = ["GetPoisonedAsync", "RequeueAsync"];

        var declared = typeof(IOutboxOperations)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();

        declared
            .Selected("operation on IOutboxOperations")
            .Where(operation => !expected.Contains(operation, StringComparer.Ordinal))
            .Select(operation =>
                $"IOutboxOperations declares '{operation}'. An operator reads what delivery gave up " +
                "on and hands one message back; anything else either deletes the evidence or " +
                "belongs to the worker that owns the table")
            .Concat(expected
                .Where(operation => !declared.Contains(operation, StringComparer.Ordinal))
                .Select(operation =>
                    $"IOutboxOperations no longer declares '{operation}', so this rule is now " +
                    "guarding a port that has moved out from under it"))
            .ShouldHold();
    }

    /// <summary>The shape the ledger identity must take: an expression-bodied string literal.</summary>
    [GeneratedRegex("ConsumerName\\s*=>\\s*\"[^\"]+\"\\s*;")]
    private static partial Regex LiteralConsumerName { get; }

    /// <summary>A ledger identity derived from the type — the unstable shapes.</summary>
    [GeneratedRegex(@"ConsumerName\s*=>[^;]*(?:nameof\s*\(|GetType\s*\()")]
    private static partial Regex DerivedConsumerName { get; }

    /// <summary>The primitives an integration event may speak: what any consumer can deserialize.</summary>
    private static bool IsPrimitiveEnough(Type type)
    {
        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;

        return unwrapped.IsPrimitive
               || unwrapped == typeof(string)
               || unwrapped == typeof(decimal)
               || unwrapped == typeof(Guid)
               || unwrapped == typeof(DateTime)
               || unwrapped == typeof(DateTimeOffset);
    }

    /// <summary>Whether the compiler wrote this type as a record.</summary>
    private static bool IsRecord(Type type) =>
        type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is not null;
}
