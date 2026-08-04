using TrainingHub.Architecture.Tests.Framework;
using NetArchTest.Rules;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What the innermost layers are allowed to know about.
/// </summary>
/// <remarks>
/// The dependency rule is the one architectural claim this repository makes on every page, and the
/// one nothing checked. It is also the claim that decays invisibly: a <c>using</c> added to fix
/// something at four in the afternoon does not look like an architectural decision, and by the time
/// it is one, the reason it was added has been forgotten.
/// </remarks>
public sealed class KernelPurityRules
{
    /// <summary>The namespaces the inner layers are defined by not knowing.</summary>
    private static readonly string[] TheOutsideWorld =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.DependencyInjection"
    ];

    /// <summary>
    /// The domain, knows neither persistence nor http.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-dependency-rule",
        "the domain knows nothing of persistence, HTTP, or the shape of the messages the API receives")]
    public void TheDomain_KnowsNeitherPersistenceNorHttp() =>
        Types.InAssembly(Solution.Domain)
            .Should()
            .NotHaveDependencyOnAny(TheOutsideWorld)
            .GetResult()
            .ShouldHold();

    /// <summary>
    /// The domain, does not know the messaging library.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-dependency-rule",
        "the domain knows nothing of the messaging library either — that trade stops at the kernel")]
    public void TheDomain_DoesNotKnowTheMessagingLibrary() =>
        // Written against directly-declared interfaces, and that is the whole difficulty. A
        // NetArchTest rule reads this the other way and fails on all six domain events: they
        // implement IDomainEvent, the kernel declares IDomainEvent as an INotification, and a
        // dependency search resolves that. The transitive reading is not wrong — the domain does
        // reach Mediator through the kernel — it is simply not the decision. ADR 0012 accepts the
        // coupling at the kernel; what must never happen is a domain type reaching for the library
        // on its own, and only the declared interfaces can tell the two apart.
        Solution.Domain.DeclaredTypes()
            .Selected("type in the domain")
            .Where(type => DeclaredInterfaces(type)
                .Any(contract => contract.Namespace?.StartsWith("Mediator", StringComparison.Ordinal) == true))
            .Select(type =>
                $"{type.FullName} implements a Mediator interface itself. Inheriting the kernel's " +
                "IDomainEvent is the recorded trade; naming the library here is a new one")
            .ShouldHold();

    /// <summary>The interfaces a type lists in its own declaration.</summary>
    /// <remarks>
    /// Reflection only offers the closure — every interface, including those reached through a base
    /// type or through another interface. Subtracting what the base type and the interfaces
    /// themselves bring leaves what the declaration actually said.
    /// </remarks>
    private static IEnumerable<Type> DeclaredInterfaces(Type type)
    {
        var all = type.GetInterfaces();

        var inherited = all
            .SelectMany(contract => contract.GetInterfaces())
            .Concat(type.BaseType?.GetInterfaces() ?? Type.EmptyTypes);

        return all.Except(inherited);
    }

    /// <summary>
    /// Only the eight recorded kernel types, touch the messaging library.
    /// </summary>
    [Fact]
    [ArchitectureRule("0012",
        "the kernel knows Mediator, and that is a recorded trade covering exactly eight types")]
    public void OnlyTheEightRecordedKernelTypes_TouchTheMessagingLibrary()
    {
        // Restating a list is normally the wrong shape for a test — the set is what the code says,
        // and a test that repeats it is wrong in the same way. Here the list *is* the decision: ADR
        // 0002 records that the coupling blocks the outbox, and ADR 0012 records that it is accepted
        // for these members and no others. A ninth type reaching for Mediator is a new decision, and
        // this is the test that says so instead of letting it arrive as a diff nobody questions.
        string[] recorded =
        [
            "IDomainEvent", "IDomainEventHandler`1",
            "ICommandBase", "ICommand`1", "ICommandHandler`2",
            "IQuery", "IQuery`1", "IQueryHandler`2"
        ];

        var touching = Solution.Kernel.DeclaredTypes()
            .Where(type => type.GetInterfaces()
                .Any(contract => contract.Namespace?.StartsWith("Mediator", StringComparison.Ordinal) == true))
            .Select(type => type.Name)
            .Selected("kernel type inheriting a Mediator interface");

        touching
            .Except(recorded, StringComparer.Ordinal)
            .Select(name =>
                $"'{name}' inherits a Mediator interface and is not one of the eight types ADR 0012 " +
                "accepts. Coupling the kernel to a messaging library is a decision; it needs a record")
            .Concat(recorded
                .Except(touching, StringComparer.Ordinal)
                .Select(name =>
                    $"'{name}' no longer inherits a Mediator interface. If the coupling is being " +
                    "removed, ADR 0012 and ADR 0002 both say something that is no longer true"))
            .ShouldHold();
    }

    /// <summary>
    /// The application layer, knows neither persistence nor http.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-dependency-rule",
        "the application layer orchestrates the domain; it does not reach for a database or a request")]
    public void TheApplicationLayer_KnowsNeitherPersistenceNorHttp()
    {
        // One assembly at a time rather than InAssemblies: each result is asserted on its own, so a
        // failure names the layer that broke the rule instead of a merged set the reader then has to
        // sort out.
        foreach (var layer in new[] { Solution.Application, Solution.LayeredApplication, Solution.CqrsApplication })
        {
            Types.InAssembly(layer)
                .Should()
                .NotHaveDependencyOnAny(TheOutsideWorld)
                .GetResult()
                .ShouldHold();
        }
    }

    /// <summary>
    /// No inner layer, registers a service.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-dependency-rule",
        "wiring is the composition root's job, and the inner layers have no composition root")]
    public void NoInnerLayer_RegistersAService()
    {
        var inner = new[]
        {
            Solution.Kernel, Solution.Domain, Solution.Application,
            Solution.LayeredApplication, Solution.CqrsApplication
        };

        foreach (var layer in inner)
        {
            Types.InAssembly(layer)
                .Should()
                .NotHaveDependencyOnAny("Microsoft.Extensions.DependencyInjection")
                .GetResult()
                .ShouldHold();
        }
    }

    /// <summary>
    /// Only the composition root, registers services.
    /// </summary>
    /// <remarks>
    /// One name is excused, and it is a pinned decision like the kernel's eight: a hosted service
    /// is a singleton by the framework's own fiat, so the outbox worker cannot inject the scoped
    /// processor it drives — opening a scope per batch is the documented pattern for exactly this
    /// shape, and it is resolution inside a boundary the host already composed, not registration
    /// (ADR 0025). Anything else in these layers reaching for the container is still a finding.
    /// </remarks>
    [Fact]
    [ArchitectureRule("README#the-dependency-rule",
        "outside the inner layers, only the extension methods a host calls may register services")]
    public void OnlyTheCompositionRoot_RegistersServices()
    {
        foreach (var layer in new[] { Solution.Infrastructure, Solution.SharedApi })
        {
            Types.InAssembly(layer)
                .That()
                .DoNotResideInNamespaceEndingWith(".Extensions")
                .And()
                .DoNotHaveName("OutboxDeliveryWorker")
                .Should()
                .NotHaveDependencyOnAny("Microsoft.Extensions.DependencyInjection")
                .GetResult()
                .ShouldHold();
        }
    }

    /// <summary>
    /// No domain type, exposes a queryable.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-dependency-rule",
        "a repository hands out aggregates; it never hands out the query that would fetch them")]
    public void NoDomainType_ExposesAQueryable() =>
        Solution.Domain.DeclaredTypes()
            .Concat(Solution.Kernel.DeclaredTypes())
            .SelectMany(type => type.GetMethods()
                .Select(method => (type, member: method.Name, signature: Signature(method)))
                .Concat(type.GetProperties()
                    .Select(property => (type, member: property.Name, signature: new[] { property.PropertyType }))))
            .Selected("member of the domain or the kernel")
            .Where(member => member.signature.Any(LeaksPersistence))
            .Select(member =>
                $"{member.type.FullName}.{member.member} names a persistence type in its signature, " +
                "so the domain would have to know how it is stored to call it")
            .ShouldHold();

    private static Type[] Signature(System.Reflection.MethodInfo method) =>
        [method.ReturnType, .. method.GetParameters().Select(parameter => parameter.ParameterType)];

    private static bool LeaksPersistence(Type type)
    {
        IEnumerable<Type> candidates = type.IsGenericType
            ? new[] { type }.Concat(type.GetGenericArguments())
            : new[] { type };

        return candidates.Any(candidate =>
            candidate.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true
            || (candidate.Namespace == "System.Linq"
                && candidate.Name.StartsWith("IQueryable", StringComparison.Ordinal))
            || (candidate.Namespace == "System.Linq"
                && candidate.Name.StartsWith("IOrderedQueryable", StringComparison.Ordinal)));
    }
}
