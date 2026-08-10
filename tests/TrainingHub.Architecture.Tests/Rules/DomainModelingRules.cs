using System.Reflection;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Common.Results;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// How the domain is modeled: what may be built, what may be changed, and by whom.
/// </summary>
/// <remarks>
/// These are the rules a reviewer applies from memory and therefore applies unevenly. Every one of
/// them holds today; what they are for is the fourth aggregate, written in a hurry by someone who
/// read three of the existing ones and inferred the convention from the two that happened to agree.
/// </remarks>
public sealed class DomainModelingRules
{
    private static IReadOnlyList<Type> DomainTypes { get; } = [.. Solution.Domain.DeclaredTypes()];

    private static IEnumerable<Type> AggregateRoots =>
        DomainTypes.Where(type => typeof(IAggregateRoot).IsAssignableFrom(type) && !type.IsAbstract);

    private static IEnumerable<Type> ValueObjects =>
        DomainTypes.Where(type => typeof(ValueObject).IsAssignableFrom(type) && !type.IsAbstract);

    private static IEnumerable<Type> DomainEvents =>
        DomainTypes.Where(type => typeof(IDomainEvent).IsAssignableFrom(type) && !type.IsAbstract);

    private static IEnumerable<Type> Identifiers =>
        DomainTypes.Where(type => Base(type, "EntityId`1") is not null);

    // ------------------------------------------------------------------ aggregates

    /// <summary>
    /// No aggregate, has a public constructor.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-domain",
        "an aggregate is created through its own factory, never by a caller with a constructor")]
    public void NoAggregate_HasAPublicConstructor() =>
        AggregateRoots
            .Selected("aggregate root")
            .Where(aggregate => aggregate.GetConstructors().Length > 0)
            .Select(aggregate =>
                $"{aggregate.Name} has a public constructor, so an invalid one can be built without " +
                "going past the rules its factory enforces")
            .ShouldHold();

    /// <summary>
    /// Every aggregate, is sealed.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-domain",
        "an aggregate is sealed: its invariants are its own, and inheritance would let someone else hold them")]
    public void EveryAggregate_IsSealed() =>
        AggregateRoots
            .Selected("aggregate root")
            .Where(aggregate => !aggregate.IsSealed)
            .Select(aggregate => $"{aggregate.Name} is not sealed")
            .ShouldHold();

    /// <summary>
    /// The questions an aggregate is allowed to answer: each wears a domain specification.
    /// </summary>
    /// <remarks>
    /// A pinned list, like the kernel's eight (ADR 0012) and the worker's carve-out: ADR 0028
    /// lets an aggregate wear a specification as a boolean question about itself —
    /// <c>Training.IsOwnedBy</c> delegates to <c>TrainingOwnedBySpecification</c> and reads
    /// nothing out. Pinned by name rather than by shape ("any bool method") on purpose, because
    /// the shape is exactly how reading state through methods would creep back in; a new
    /// question is a new decision, and it arrives here with its record.
    /// </remarks>
    private static readonly (string Aggregate, string Method)[] SpecificationQuestions =
    [
        ("Training", "IsOwnedBy"),
    ];

    /// <summary>
    /// No aggregate, returns data.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-domain",
        "an aggregate answers whether a change was allowed; it never hands data back")]
    public void NoAggregate_ReturnsData() =>
        AggregateRoots
            .SelectMany(aggregate => aggregate
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => (aggregate, method)))
            .Selected("public method declared on an aggregate")
            .Where(pair => !AnswersOnlyWhetherItWorked(pair.method.ReturnType)
                && !SpecificationQuestions.Contains((pair.aggregate.Name, pair.method.Name)))
            .Select(pair =>
                $"{pair.aggregate.Name}.{pair.method.Name} returns {pair.method.ReturnType.Name}. An " +
                "aggregate that hands data back invites callers to read through it rather than " +
                "through the read side")
            .ShouldHold();

    /// <summary>
    /// No aggregate, holds another aggregate.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-domain",
        "an aggregate holds another aggregate by identifier, never by reference")]
    public void NoAggregate_HoldsAnotherAggregate() =>
        AggregateRoots
            .SelectMany(aggregate => aggregate.GetProperties().Select(property => (aggregate, property)))
            .Selected("property on an aggregate")
            .Where(pair => typeof(IAggregateRoot).IsAssignableFrom(pair.property.PropertyType))
            .Select(pair =>
                $"{pair.aggregate.Name}.{pair.property.Name} is a reference to another aggregate. " +
                "Two aggregates in one object graph are two transactions pretending to be one")
            .ShouldHold();

    /// <summary>
    /// No domain type, hands out a mutable collection.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-domain",
        "a collection leaves the domain read-only, or it is not the domain that owns it")]
    public void NoDomainType_HandsOutAMutableCollection() =>
        DomainTypes
            .SelectMany(type => type.GetProperties().Select(property => (type, property)))
            .Selected("property on a domain type")
            .Where(pair => IsMutableCollection(pair.property.PropertyType))
            .Select(pair =>
                $"{pair.type.Name}.{pair.property.Name} is a {pair.property.PropertyType.Name}, which " +
                "a caller can add to without the aggregate ever knowing")
            .ShouldHold();

    /// <summary>
    /// No domain type, has a public setter.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-domain",
        "state changes go through behavior, so no property is settable from outside")]
    public void NoDomainType_HasAPublicSetter() =>
        DomainTypes
            // Records are exempt, and only records: their init accessors are the syntax, and a
            // positional record with private ones cannot be declared at all. Every other domain type
            // is a class whose accessors were chosen one at a time.
            .Where(type => !IsRecord(type))
            .SelectMany(type => type.GetProperties().Select(property => (type, property)))
            .Selected("property on a non-record domain type")
            .Where(pair => pair.property.SetMethod?.IsPublic == true)
            .Select(pair =>
                $"{pair.type.Name}.{pair.property.Name} has a public setter. An init accessor counts: " +
                "reflection sees an ordinary set method wearing a required modifier")
            .ShouldHold();

    /// <summary>
    /// The recorded domain services: each one a decision no aggregate can own, admitted by its
    /// own record (ADR 0036), never by habit.
    /// </summary>
    private static readonly (string Type, string Record)[] RecordedDomainServices =
    [
        ("TrainingTransferDomainService", "0036"),
    ];

    /// <summary>
    /// The domain, names no service.
    /// </summary>
    /// <remarks>
    /// The drift this stops has a shape: a rule needs a fact the aggregate cannot see, somebody
    /// reaches for a <c>TrainingCreationService</c>, and from that day creation rules have two
    /// homes — the ones in the factory, and the ones a caller must remember to invoke. Both rules
    /// of that kind today (title uniqueness, catalog capacity) come to the factory through a
    /// port instead, so the factory's signature is the complete list of what creation asks. The
    /// one admission is a decision that has no home at all — pinned by name and record, exactly
    /// as the aggregate-question rule pins <c>IsOwnedBy</c>; anything unpinned still fails here.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0030",
        "a rule the aggregate cannot settle alone comes to it through a port — the domain declares no service to decide in its place")]
    public void TheDomain_NamesNoService() =>
        DomainTypes
            .Selected("domain type")
            .Where(type => type.Name.EndsWith("Service", StringComparison.Ordinal)
                           && RecordedDomainServices.All(pin => pin.Type != type.Name))
            .Select(type =>
                $"{type.Name} is a service declared in the domain. Bring the fact to the aggregate " +
                "through a port, as IUniquenessTitleChecker and ITrainingCounter do — a service " +
                "deciding beside the aggregate is a rule a caller can forget to apply. A decision " +
                "with no home arrives with a record of its own, as TrainingTransferDomainService did (ADR 0036)")
            .ShouldHold();

    /// <summary>
    /// Every domain service, exists by record.
    /// </summary>
    /// <remarks>
    /// The other half of the admission: the ledger above lets a recorded service through, and
    /// this rule holds every service that got through to ADR 0036's shape — recorded, named a
    /// <c>DomainService</c> so a reader can tell it from an application or infrastructure service
    /// without opening the file, static so the domain names no lifetime, stateless, and answering
    /// only whether the transfer of authority worked. A service that drifts from the shape is on
    /// its way to the anemic façade 0030 warned about, and fails here before it arrives.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0036",
        "a domain service exists only for a decision no aggregate can own: recorded, named a DomainService, static, stateless, its ports arriving as parameters")]
    public void EveryDomainService_ExistsByRecord() =>
        DomainTypes
            .Where(type => type.Name.EndsWith("Service", StringComparison.Ordinal))
            .Selected("domain service")
            .SelectMany(service =>
            {
                var violations = new List<string>();

                if (RecordedDomainServices.All(pin => pin.Type != service.Name))
                {
                    violations.Add($"{service.Name} is a domain service no record admits. A decision " +
                        "with no home is a new decision — write the record, then pin it here");
                }

                if (!service.Name.EndsWith("DomainService", StringComparison.Ordinal))
                {
                    violations.Add($"{service.Name} does not end in DomainService. The suffix is the " +
                        "convention that tells a reader, at the name alone, that this is a domain " +
                        "service in the DDD sense — not an application, infrastructure or any other " +
                        "kind of service (ADR 0036)");
                }

                if (!(service.IsAbstract && service.IsSealed))
                {
                    violations.Add($"{service.Name} is not static. A domain service is a stateless " +
                        "decision, and the domain names no lifetime (ADR 0036)");
                }

                if (service.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Length > 0)
                {
                    violations.Add($"{service.Name} carries instance state. The ports arrive as " +
                        "parameters, so the signature is the complete inventory of what the decision asks");
                }

                violations.AddRange(service
                    .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(method => method.ReturnType != typeof(Result)
                                     && method.ReturnType != typeof(Task<Result>))
                    .Select(method =>
                        $"{service.Name}.{method.Name} answers {method.ReturnType.Name}. A domain " +
                        "service decides — it answers whether the operation was allowed, never data"));

                return violations;
            })
            .ShouldHold();

    // ------------------------------------------------------------------ value objects

    /// <summary>
    /// Every value object, is sealed.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#value-objects",
        "a value object is sealed and immutable, or it is not a value")]
    public void EveryValueObject_IsSealed() =>
        ValueObjects
            .Selected("value object")
            .Where(valueObject => !valueObject.IsSealed)
            .Select(valueObject => $"{valueObject.Name} is not sealed")
            .ShouldHold();

    /// <summary>
    /// Every value object, has a private constructor for the orm.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#value-objects",
        "a value object keeps a parameterless constructor for the ORM, and keeps it private")]
    public void EveryValueObject_HasAPrivateConstructorForTheOrm() =>
        ValueObjects
            .Selected("value object")
            .Where(valueObject => valueObject
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .All(constructor => constructor.GetParameters().Length != 0))
            .Select(valueObject =>
                $"{valueObject.Name} declares no private parameterless constructor. EF Core needs one " +
                "to materialize it, and it fails at query time rather than at build time")
            .ShouldHold();

    /// <summary>
    /// Every value object, is built through a factory that can refuse.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#value-objects",
        "a value object is built through a factory that can refuse — Create returning a Result, or a "
        + "TryFrom — unless it is a closed enumeration, whose instances are all it has")]
    public void EveryValueObject_IsBuiltThroughAFactoryThatCanRefuse() =>
        ValueObjects
            .Selected("value object")
            .Where(valueObject => !HasResultFactory(valueObject)
                                  && !HasTryFrom(valueObject)
                                  && !IsClosedEnumeration(valueObject))
            .Select(valueObject =>
                $"{valueObject.Name} offers neither a static Create returning Result<{valueObject.Name}> " +
                "nor a TryFrom, and is not a closed enumeration either — so a caller has some way of " +
                "building one this type never approved")
            .ShouldHold();

    // ------------------------------------------------------------------ identifiers

    /// <summary>
    /// Every identifier, declares the constructor its factory needs.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#typed-identifiers",
        "every identifier declares the private constructor its factory reflects for")]
    public void EveryIdentifier_DeclaresTheConstructorItsFactoryNeeds() =>
        Identifiers
            .Selected("typed identifier")
            .Where(identifier => identifier.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null,
                types: [typeof(Guid)],
                modifiers: null) is null)
            .Select(identifier =>
                $"{identifier.Name} declares no constructor taking a single Guid. EntityId compiles a " +
                "factory per type by reflection, inside a static field initializer — so this is not a " +
                "compile error, it is a TypeInitializationException on first use, in production")
            .ShouldHold();

    /// <summary>
    /// Every identifier, can actually be created.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#typed-identifiers",
        "and the factory runs: the constructor being there is not the same as the factory working")]
    public void EveryIdentifier_CanActuallyBeCreated() =>
        Identifiers
            .Selected("typed identifier")
            .Select(identifier => (identifier, failure: TryCreate(identifier)))
            .Where(attempt => attempt.failure is not null)
            .Select(attempt => $"{attempt.identifier.Name}.Create(Guid) threw: {attempt.failure}")
            .ShouldHold();

    /// <summary>
    /// No identifier, converts implicitly.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#typed-identifiers",
        "a Guid becomes an identifier only when someone says so, and an identifier never silently becomes a Guid")]
    public void NoIdentifier_ConvertsImplicitly() =>
        Identifiers
            .Append(typeof(EntityId<>))
            .Selected("identifier type")
            .SelectMany(identifier => identifier
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => method.Name == "op_Implicit")
                .Select(method => $"{identifier.Name} declares an implicit conversion"))
            .ShouldHold();

    // ------------------------------------------------------------------ domain events

    /// <summary>
    /// Every domain event, is a sealed record.
    /// </summary>
    [Fact]
    [ArchitectureRule("0002",
        "a domain event is an immutable record of something that already happened")]
    public void EveryDomainEvent_IsASealedRecord() =>
        DomainEvents
            .Selected("domain event")
            .Where(domainEvent => !domainEvent.IsSealed || !IsRecord(domainEvent))
            .Select(domainEvent => $"{domainEvent.Name} is not a sealed record")
            .ShouldHold();

    /// <summary>
    /// Every domain event, carries only domain types.
    /// </summary>
    [Fact]
    [ArchitectureRule("0002",
        "a domain event carries the domain's own vocabulary, not the primitives underneath it")]
    public void EveryDomainEvent_CarriesOnlyDomainTypes() =>
        DomainEvents
            .SelectMany(domainEvent => domainEvent
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => (domainEvent, property)))
            .Selected("property on a domain event")
            .Where(pair => !typeof(ValueObject).IsAssignableFrom(pair.property.PropertyType)
                           && Base(pair.property.PropertyType, "EntityId`1") is null)
            .Select(pair =>
                $"{pair.domainEvent.Name}.{pair.property.Name} is a {pair.property.PropertyType.Name}. " +
                "An event carrying a string carries no meaning a handler can rely on")
            .ShouldHold();

    /// <summary>
    /// Every domain event, has a handler.
    /// </summary>
    [Fact]
    [ArchitectureRule("0002",
        "every domain event is reacted to; one nobody handles is a decision that was never finished")]
    public void EveryDomainEvent_HasAHandler()
    {
        var handled = Solution.Application.DeclaredTypes()
            .SelectMany(handler => handler.GetInterfaces())
            .Where(contract => contract.IsGenericType
                               && contract.GetGenericTypeDefinition().Name.StartsWith("IDomainEventHandler", StringComparison.Ordinal))
            .Select(contract => contract.GetGenericArguments()[0])
            .ToHashSet();

        DomainEvents
            .Selected("domain event")
            .Where(domainEvent => !handled.Contains(domainEvent))
            .Select(domainEvent =>
                $"nothing handles {domainEvent.Name}. It is raised into the unit of work and dropped")
            .ShouldHold();
    }

    /// <summary>
    /// No domain event handler, commits.
    /// </summary>
    [Fact]
    [ArchitectureRule("0002",
        "handlers run inside the transaction the aggregate is being saved in, so none of them commits")]
    public void NoDomainEventHandler_Commits() =>
        Solution.Application.DeclaredTypes()
            .Where(handler => handler.GetInterfaces().Any(contract =>
                contract.IsGenericType
                && contract.GetGenericTypeDefinition().Name.StartsWith("IDomainEventHandler", StringComparison.Ordinal)))
            .Selected("domain event handler")
            .SelectMany(handler => handler.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.ParameterType.Name is "IUnitOfWork")
                .Select(_ => $"{handler.Name} takes IUnitOfWork. The dispatcher already runs inside " +
                             "the unit of work; committing here commits half of it"))
            .ShouldHold();

    /// <summary>
    /// Raising a domain event, stays inside the aggregate.
    /// </summary>
    [Fact]
    [ArchitectureRule("0002",
        "only an aggregate raises a domain event, which is why the method that raises one is not public")]
    public void RaisingADomainEvent_StaysInsideTheAggregate() =>
        typeof(AggregateRoot<>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name.StartsWith("AddDomainEvent", StringComparison.Ordinal))
            .Select(method =>
                $"AggregateRoot.{method.Name} is public, so anything holding an aggregate can make it " +
                "claim something happened")
            .ShouldHold();

    // ------------------------------------------------------------------ results and ports

    /// <summary>
    /// Result, offers no unchecked way in.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-result-type",
        "Result offers no way to read a value without handling the failure: that is the whole point of it")]
    public void Result_OffersNoUncheckedWayIn()
    {
        string[] escapeHatches =
            ["IsSuccess", "IsFailure", "HasErrors", "Value", "Error", "Errors", "Unwrap", "GetValueOrDefault"];

        new[] { typeof(Result), typeof(Result<>) }
            .SelectMany(result => result
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(member => (result, member)))
            .Selected("public member of Result")
            .Where(pair => escapeHatches.Contains(pair.member.Name, StringComparer.Ordinal))
            .Select(pair =>
                $"{pair.result.Name} exposes {pair.member.Name}. Match and Switch exist so that a " +
                "failure cannot be ignored by accident; one property is all it takes to undo that")
            .ShouldHold();

    }

    /// <summary>
    /// Every asynchronous port, is named and cancellable.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#repository-conventions",
        "every asynchronous port takes a cancellation token, and says so in its name")]
    public void EveryAsynchronousPort_IsNamedAndCancellable() =>
        Solution.Domain.DeclaredTypes()
            .Concat(Solution.Kernel.DeclaredTypes())
            .Where(type => type.IsInterface)
            .SelectMany(port => port
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => IsAsynchronous(method.ReturnType))
                .Select(method => (port, method)))
            .Selected("asynchronous method on a domain port")
            .Where(pair => !pair.method.Name.EndsWith("Async", StringComparison.Ordinal)
                           || pair.method.GetParameters().LastOrDefault()?.ParameterType != typeof(CancellationToken))
            .Select(pair =>
                $"{pair.port.Name}.{pair.method.Name} is asynchronous but does not both end in Async " +
                "and take a CancellationToken last")
            .ShouldHold();

    // ------------------------------------------------------------------ helpers

    private static bool AnswersOnlyWhetherItWorked(Type returnType)
    {
        if (returnType == typeof(void) || returnType == typeof(Result))
        {
            return true;
        }

        return returnType.IsGenericType
               && returnType.GetGenericTypeDefinition().Name.StartsWith("Task", StringComparison.Ordinal)
               && returnType.GetGenericArguments()[0] == typeof(Result);
    }

    private static bool IsAsynchronous(Type returnType) =>
        returnType.Name.StartsWith("Task", StringComparison.Ordinal)
        || returnType.Name.StartsWith("ValueTask", StringComparison.Ordinal);

    private static bool IsMutableCollection(Type type) =>
        type.IsGenericType
        && type.GetGenericTypeDefinition().Name is "List`1" or "ICollection`1" or "IList`1" or "HashSet`1";

    /// <summary>Whether the compiler wrote this type as a record.</summary>
    private static bool IsRecord(Type type) =>
        type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is not null;

    private static Type? Base(Type type, string genericName)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition().Name == genericName)
            {
                return current;
            }
        }

        return null;
    }

    private static bool HasResultFactory(Type valueObject) =>
        valueObject
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Any(method => method.Name == "Create"
                           && method.ReturnType.IsGenericType
                           && method.ReturnType.GetGenericTypeDefinition() == typeof(Result<>)
                           && method.ReturnType.GetGenericArguments()[0] == valueObject);

    private static bool HasTryFrom(Type valueObject) =>
        valueObject
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Any(method => method.Name.StartsWith("TryFrom", StringComparison.Ordinal)
                           && method.ReturnType == typeof(bool));

    /// <summary>
    /// Whether the type is a closed enumeration: every instance there will ever be is one of its own
    /// public static readonly fields, and no constructor is reachable to make another.
    /// </summary>
    /// <remarks>
    /// The exemption the factory rule grants, stated as a shape rather than as a list of names. What
    /// that rule is really defending is that no caller can hold an invalid instance, and a closed
    /// enumeration answers it more strongly than a factory does — a factory can refuse, whereas here
    /// there is nothing to refuse, because the only values are the ones the type declared.
    /// <para>
    /// It used to name <c>Topic</c> as the single recorded exception, which turned the next closed
    /// set into a choice between two bad options: widen a list by hand, or add a <c>TryFrom</c> that
    /// exists only to satisfy a rule. <c>Topic</c> keeps its <c>TryFromName</c> because a client
    /// genuinely names a topic; nobody names a <c>TrainingStatus</c>, so one there would be dead
    /// code this rule had invented.
    /// </para>
    /// </remarks>
    private static bool IsClosedEnumeration(Type valueObject) =>
        valueObject.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Length == 0
        && valueObject
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Any(field => field.IsInitOnly && field.FieldType == valueObject);

    private static string? TryCreate(Type identifier)
    {
        var create = identifier.GetMethod(
            "Create",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
            binder: null,
            types: [typeof(Guid)],
            modifiers: null);

        if (create is null)
        {
            return "there is no static Create(Guid) to call";
        }

        try
        {
            return create.Invoke(null, [Guid.NewGuid()]) is null ? "it returned null" : null;
        }
        catch (Exception exception)
        {
            return (exception.InnerException ?? exception).Message;
        }
    }
}
