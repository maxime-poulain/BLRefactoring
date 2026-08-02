using BLRefactoring.Architecture.Tests.Framework;
using Xunit;

namespace BLRefactoring.Architecture.Tests.Rules;

/// <summary>
/// Inheritance is a decision, so a class that nobody inherits says so.
/// </summary>
/// <remarks>
/// A class left open is a promise of extensibility nobody made and nobody keeps. It permits an
/// inheritance the type was never designed to survive, and it stops a reader telling a deliberate
/// extension point from a type that simply never got the keyword. The repository applied this
/// unevenly for a long time — sealed on one request contract and not on the one beside it — which is
/// what a convention with nothing holding it always looks like.
/// </remarks>
public sealed class SealingRules
{
    /// <summary>
    /// Every type this suite can see, across every assembly it references.
    /// </summary>
    /// <remarks>
    /// The scope is the whole point, and it is why this rule is written by hand rather than with
    /// NetArchTest's <c>AreNotInheritedByAnyType</c>. That predicate exists in the fork and would be
    /// the natural instrument, but it can only see the types loaded into the scan it runs in: applied
    /// one assembly at a time it would call a domain class uninherited when its only subclass lives
    /// in infrastructure, and demand a <c>sealed</c> that does not compile.
    /// <para>
    /// "Nobody inherits it" is a statement about everything, so it can only be made by something that
    /// sees everything — including the test projects, since a production class whose only subclass is
    /// a test double cannot be sealed either. That is why this project references the whole solution.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<Type> Everything { get; } =
    [
        .. Solution.All.SelectMany(assembly => assembly.DeclaredTypes())
    ];

    private static IReadOnlySet<Type> Inherited { get; } =
        Everything
            .Select(type => type.BaseType)
            .Where(baseType => baseType is not null)
            // A generic base is recorded by its open definition: TestAggregate derives from
            // AggregateRoot<TestAggregateId>, and what that keeps open is AggregateRoot<>.
            .Select(baseType => baseType!.IsGenericType ? baseType.GetGenericTypeDefinition() : baseType)
            .ToHashSet();

    [Fact]
    [ArchitectureRule("0014",
        "a class nobody inherits is sealed, so that an open one means somebody meant it")]
    public void EveryClassNobodyInherits_IsSealed() =>
        Everything
            // Abstract covers static too — a static class is abstract and sealed at once — and
            // interfaces, records and value types are not what this rule is about.
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Selected("concrete class")
            .Where(type => !type.IsSealed && !Inherited.Contains(type))
            .Select(type =>
                $"{type.FullName} is open and nothing derives from it. Either seal it, or the type " +
                "that inherits it is missing")
            .ShouldHold();

    [Fact]
    [ArchitectureRule("0014",
        "an abstract class exists to be inherited, so one that nobody inherits is dead")]
    public void EveryAbstractClass_IsActuallyInherited() =>
        // Not the converse of the rule above — that would be the same statement twice — but the
        // other half of the same idea. Sealing says "this is not an extension point"; an abstract
        // class says "this is nothing but an extension point", and one nobody extends is a base for
        // a hierarchy that was removed, or never arrived.
        Everything
            .Where(type => type is { IsClass: true, IsAbstract: true, IsSealed: false })
            .Selected("abstract class")
            .Where(type => !Inherited.Contains(type))
            .Select(type =>
                $"{type.FullName} is abstract and nothing derives from it — a base class with no " +
                "hierarchy under it")
            .ShouldHold();
}
