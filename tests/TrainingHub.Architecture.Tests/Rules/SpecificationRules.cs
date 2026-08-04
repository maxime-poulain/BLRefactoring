using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Common.Specifications;
using NetArchTest.Rules;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What a specification is here, held in place: a named business rule, or nothing.
/// </summary>
/// <remarks>
/// ADR 0028 requalified a machinery that had drifted into a query DSL — generic
/// specification-taking repository members, a filter with no rule content wearing a domain name.
/// The pattern's failure mode is not dramatic, it is gradual: one convenient criteria object at a
/// time, until the domain vocabulary is a thin wrapper over <c>Where</c>. These rules are the
/// ratchet against the slide.
/// </remarks>
public sealed partial class SpecificationRules
{
    /// <summary>The home of a specification: the Specifications namespace of one aggregate.</summary>
    [GeneratedRegex(@"^TrainingHub\.Shared\.Domain\.Aggregates\.\w+Aggregate\.Specifications$")]
    private static partial Regex AggregateSpecificationsNamespace { get; }

    /// <summary>Every concrete specification the backend declares.</summary>
    private static IEnumerable<Type> Specifications =>
        Solution.Backend
            .SelectMany(assembly => assembly.DeclaredTypes())
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .Where(type => type.GetInterfaces().Any(contract =>
                contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ISpecification<>)));

    /// <summary>
    /// Every specification, is declared in the domain, beside its aggregate.
    /// </summary>
    /// <remarks>
    /// A specification states a business rule, and business rules live in the domain — an
    /// application-layer or infrastructure specification would be a rule the domain does not
    /// know it has. "Beside its aggregate" is the namespace shape making the owner readable:
    /// the rule about trainings sits with <c>Training</c>, not in a shared bucket.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0028",
        "a specification names a business rule, so it is declared in the domain, in the Specifications namespace of the aggregate it speaks about")]
    public void EverySpecification_IsDeclaredInTheDomain() =>
        Specifications
            .Selected("specification")
            .Where(specification =>
                specification.Assembly != Solution.Domain
                || specification.Namespace is null
                || !AggregateSpecificationsNamespace.IsMatch(specification.Namespace))
            .Select(specification =>
                $"{specification.FullName} implements ISpecification<T> outside the domain's " +
                "aggregate Specifications namespaces. A rule the domain does not declare is a rule " +
                "the domain does not have — move it beside the aggregate it speaks about")
            .ShouldHold();

    /// <summary>
    /// No query handler, touches a specification.
    /// </summary>
    /// <remarks>
    /// The boundary this repository chose on purpose: the CQRS readers ask their questions in SQL,
    /// shaped for a screen, and a specification never filters a query-side read. A reader that
    /// borrows a domain rule to scope its data has quietly made the read side depend on the write
    /// model's vocabulary — the exact coupling the two-sided design exists to avoid.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0028",
        "the CQRS readers read directly; no specification ever filters a query-side read")]
    public void NoQueryHandler_TouchesASpecification() =>
        Types.InAssembly(Solution.CqrsInfrastructure)
            .Should()
            .NotHaveDependencyOnAny("TrainingHub.Shared.Common.Specifications")
            .GetResult()
            .ShouldHold();

    /// <summary>
    /// No repository contract, takes a specification.
    /// </summary>
    /// <remarks>
    /// The lock on the door the DSL came through: <c>IRepository&lt;T&gt;</c> used to expose
    /// <c>GetAsync(ISpecification)</c> to every caller, which made arbitrary criteria composition
    /// an application-layer liberty. A repository's surface is the list of questions the use
    /// cases actually ask, each with a name; specifications are consumed by implementations,
    /// never passed through contracts.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0028",
        "a repository's surface is named questions; no domain contract passes a specification through")]
    public void NoRepositoryContract_TakesASpecification() =>
        Solution.Domain.DeclaredTypes()
            .Where(type => type.IsInterface)
            .SelectMany(contract => contract.GetMethods()
                .Select(method => (contract, method)))
            .Selected("domain interface method")
            .Where(member => Mentions(member.method.ReturnType)
                || member.method.GetParameters().Any(parameter => Mentions(parameter.ParameterType)))
            .Select(member =>
                $"{member.contract.FullName}.{member.method.Name} names ISpecification in its " +
                "signature. A specification is how a rule is stated, not how a query is ordered — " +
                "give the question a name on the contract and consume the rule inside the adapter")
            .ShouldHold();

    private static bool Mentions(Type type)
    {
        IEnumerable<Type> candidates = type.IsGenericType
            ? new[] { type }.Concat(type.GetGenericArguments())
            : [type];

        return candidates.Any(candidate =>
            (candidate.IsGenericType ? candidate.GetGenericTypeDefinition() : candidate) == typeof(ISpecification<>));
    }
}
