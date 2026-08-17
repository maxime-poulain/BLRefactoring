using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What a handler's name must say: what it does, and which event — in full — makes it do it.
/// </summary>
/// <remarks>
/// The two families of handler run at opposite moments under opposite rules — a domain event
/// handler inside the transaction, an integration event consumer after the commit (ADR 0002) — and
/// the word that tells them apart is exactly the word the domain handlers used to drop:
/// <c>DeleteTrainingWhenTrainerDeletedEventHandler</c> handled <c>TrainerDeletedDomainEvent</c> and
/// said <c>EventHandler</c> where the event said <c>DomainEvent</c>. Embedding the event's full
/// type name is what makes the suffix fall out for free on both families, keeps the policy phrase
/// the event storming maps onto files, and leaves room for the events that have two handlers —
/// which a bare <c>{Event}DomainEventHandler</c> could never name twice (ADR 0087).
/// </remarks>
public sealed class HandlerNamingRules
{
    /// <summary>
    /// Every handler, is named for the event it handles.
    /// </summary>
    /// <remarks>
    /// The population is taken by reflection over the closed handler interfaces, so the event a
    /// handler answers is read off its contract rather than guessed from its name — and a handler
    /// answering two events is judged once per event, since each is a claim its name must carry.
    /// Suffix-only enforcement was rejected in the record: <c>SomeDomainEventHandler</c> wears the
    /// right ending and says nothing.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0087",
        "a handler is named {Reaction}When{Event}Handler with the event's full type name embedded, " +
        "so the name alone says what it does, when it runs, and under which kind of event's rules")]
    public void EveryHandler_IsNamedForTheEventItHandles() =>
        Solution.Backend
            .SelectMany(assembly => assembly.DeclaredTypes())
            .Where(type => type is { IsInterface: false, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType
                                   && (contract.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>)
                                       || contract.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)))
                .Select(contract => (Handler: type, Event: contract.GetGenericArguments()[0])))
            .Selected("event handler")
            .Where(entry => !entry.Handler.Name.EndsWith(
                $"When{entry.Event.Name}Handler", StringComparison.Ordinal))
            .Select(entry =>
                $"{entry.Handler.Name} handles {entry.Event.Name} and does not end with " +
                $"When{entry.Event.Name}Handler. The name should say what it does and then name " +
                "the event in full — the suffix that classifies the handler comes with it")
            .ShouldHold();
}
