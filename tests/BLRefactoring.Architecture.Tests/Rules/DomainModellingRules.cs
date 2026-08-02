using System.Reflection;
using BLRefactoring.Architecture.Tests.Framework;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Results;
using Xunit;

namespace BLRefactoring.Architecture.Tests.Rules;

/// <summary>
/// How the domain is modelled: what may be built, what may be changed, and by whom.
/// </summary>
/// <remarks>
/// These are the rules a reviewer applies from memory and therefore applies unevenly. Every one of
/// them holds today; what they are for is the fourth aggregate, written in a hurry by someone who
/// read three of the existing ones and inferred the convention from the two that happened to agree.
/// </remarks>
public sealed class DomainModellingRules
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

    [Fact]
    [ArchitectureRule("README#the-domain",
        "an aggregate is sealed: its invariants are its own, and inheritance would let someone else hold them")]
    public void EveryAggregate_IsSealed() =>
        AggregateRoots
            .Selected("aggregate root")
            .Where(aggregate => !aggregate.IsSealed)
            .Select(aggregate => $"{aggregate.Name} is not sealed")
            .ShouldHold();

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
            .Where(pair => !AnswersOnlyWhetherItWorked(pair.method.ReturnType))
            .Select(pair =>
                $"{pair.aggregate.Name}.{pair.method.Name} returns {pair.method.ReturnType.Name}. An " +
                "aggregate that hands data back invites callers to read through it rather than " +
                "through the read side")
            .ShouldHold();

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

    [Fact]
    [ArchitectureRule("README#the-domain",
        "state changes go through behaviour, so no property is settable from outside")]
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

    // ------------------------------------------------------------------ value objects

    [Fact]
    [ArchitectureRule("README#value-objects",
        "a value object is sealed and immutable, or it is not a value")]
    public void EveryValueObject_IsSealed() =>
        ValueObjects
            .Selected("value object")
            .Where(valueObject => !valueObject.IsSealed)
            .Select(valueObject => $"{valueObject.Name} is not sealed")
            .ShouldHold();

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
                "to materialise it, and it fails at query time rather than at build time")
            .ShouldHold();

    [Fact]
    [ArchitectureRule("README#value-objects",
        "a value object is built through a factory that can refuse: Create returning a Result, or a TryFrom")]
    public void EveryValueObject_IsBuiltThroughAFactoryThatCanRefuse() =>
        ValueObjects
            .Selected("value object")
            .Where(valueObject => !HasResultFactory(valueObject) && !HasTryFrom(valueObject))
            .Select(valueObject =>
                $"{valueObject.Name} offers neither a static Create returning Result<{valueObject.Name}> " +
                "nor a TryFrom. Topic is the recorded exception: it is a closed set, so the only way " +
                "in is by name")
            .ShouldHold();

    // ------------------------------------------------------------------ identifiers

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
                "factory per type by reflection, inside a static field initialiser — so this is not a " +
                "compile error, it is a TypeInitializationException on first use, in production")
            .ShouldHold();

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

    [Fact]
    [ArchitectureRule("0002",
        "a domain event is an immutable record of something that already happened")]
    public void EveryDomainEvent_IsASealedRecord() =>
        DomainEvents
            .Selected("domain event")
            .Where(domainEvent => !domainEvent.IsSealed || !IsRecord(domainEvent))
            .Select(domainEvent => $"{domainEvent.Name} is not a sealed record")
            .ShouldHold();

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
