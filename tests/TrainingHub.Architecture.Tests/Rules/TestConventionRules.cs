using System.Reflection;
using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// How the test suites are built, and the two mechanisms nothing else was watching.
/// </summary>
/// <remarks>
/// ADR 0007 records that the assertion library had drifted once already, and that the drift spread
/// the way drift does: a new shared test copied the file next to it. The record fixed the drift and
/// wrote down the decision; this is the part that keeps it fixed.
/// </remarks>
public sealed partial class TestConventionRules
{
    // A call site, not the word: the capital after the dot is what distinguishes Assert.Equal from
    // an "// Assert." at the end of a comment, and the lookbehind keeps AwesomeAssertions from
    // matching itself.
    [GeneratedRegex(@"(?<![\w.])Assert\.[A-Z]")]
    private static partial Regex AssertCall { get; }

    // Spelled in two halves on purpose. This rule forbids a token, and a rule that spells the token
    // it forbids fails on its own source — which would be funny once and then permanently in the way.
    private const string RejectedLibrary = "Fluent" + "Assertions";

    /// <summary>
    /// The rejected assertion library, is named only where it is ruled out.
    /// </summary>
    [Fact]
    [ArchitectureRule("0007",
        "the assertion library is AwesomeAssertions, and the one it replaced appears nowhere")]
    public void TheRejectedAssertionLibrary_IsNamedOnlyWhereItIsRuledOut() =>
        SourceTree.SourceFiles
            .Concat(SourceTree.ProjectFiles)
            .Concat([Path.Combine(SourceTree.RepositoryRoot, "Directory.Packages.props")])
            .Where(File.Exists)
            .Selected("file that could name the package")
            .Where(file => SourceTree.ReadText(file).Contains(RejectedLibrary, StringComparison.Ordinal))
            .Select(SourceTree.Relative)
            // Two legitimate mentions, both prose about a rejected choice, which is how this
            // repository keeps rejected choices legible: the comment in Directory.Packages.props
            // explaining why the fork is used, and this file, which cannot state the rule without
            // naming what it rules out.
            .Where(file => file != "Directory.Packages.props" && !StatesTheseRules(file))
            .Select(file =>
                $"'{file}' names the library ADR 0007 replaced with a fork on a permissive licence")
            .ShouldHold();

    /// <summary>
    /// No test, calls xunit assert.
    /// </summary>
    [Fact]
    [ArchitectureRule("0007",
        "subject.Should() everywhere, and no Assert.* anywhere")]
    public void NoTest_CallsXunitAssert() =>
        SourceTree.SourceFiles
            .Where(file => SourceTree.Relative(file).StartsWith("tests/", StringComparison.Ordinal))
            .Selected("file under tests/")
            .Select(SourceTree.Relative)
            .Where(file => !StatesTheseRules(file))
            .Where(file => SourceTree.ReadLines(Path.Combine(SourceTree.RepositoryRoot, file))
                .Any(line => AssertCall.IsMatch(line)))
            .Select(file =>
                $"'{file}' calls the assertion class ADR 0007 ruled out. The kit drifted to it once " +
                "already, and the file next to it copied the drift")
            .ShouldHold();

    /// <summary>
    /// Whether this is the file that states these rules, and is therefore allowed to name what they forbid.
    /// </summary>
    /// <remarks>
    /// Two rules here forbid a token, and neither can be written — nor explained in a comment beside
    /// itself — without spelling it. Both went red on this file before the exemption existed, which
    /// is a cheap demonstration that the file scan reads what it claims to.
    /// </remarks>
    private static bool StatesTheseRules(string relativePath) =>
        relativePath.EndsWith("TestConventionRules.cs", StringComparison.Ordinal);

    /// <summary>
    /// Every test project, carries the assertion library.
    /// </summary>
    [Fact]
    [ArchitectureRule("0007",
        "every test project carries the assertion library, the kit included")]
    public void EveryTestProject_CarriesTheAssertionLibrary() =>
        ProjectGraph.Projects
            .Where(project => project.RelativePath.StartsWith("tests/", StringComparison.Ordinal))
            .Selected("project under tests/")
            .Where(project => project.PackageReferences.All(package => package.Name != "AwesomeAssertions"))
            .Select(project =>
                $"{project.RelativePath} declares no AwesomeAssertions. ADR 0007 names the kit " +
                "explicitly, because the kit is where the drift started")
            .ShouldHold();

    /// <summary>
    /// The shared kit, is not selected by the slow workflow.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#continuous-integration",
        "the two workflow filters are exact inverses, and they key on a test's fully-qualified name")]
    public void TheSharedKit_IsNotSelectedByTheSlowWorkflow() =>
        Solution.TestKit.DeclaredTypes()
            .Selected("type in the shared kit")
            .Where(type => type.FullName?.Contains("IntegrationTests", StringComparison.Ordinal) == true)
            .Select(type =>
                $"{type.FullName} carries 'IntegrationTests' in its name. The filters select on the " +
                "fully-qualified name, not on the project, so this moves the kit's tests between " +
                "workflows without anyone editing a workflow")
            .ShouldHold();

    /// <summary>
    /// Every shared test base, is abstract and generic.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#testing",
        "a shared test base is abstract, generic in its factory, and named for what it is")]
    public void EverySharedTestBase_IsAbstractAndGeneric() =>
        Solution.TestKit.DeclaredTypes()
            .Where(DeclaresTests)
            .Selected("type declaring tests in the shared kit")
            .Where(type => !type.IsAbstract
                           || !type.IsGenericTypeDefinition
                           || !StripArity(type.Name).EndsWith("Test", StringComparison.Ordinal))
            .Select(type =>
                $"{type.Name} declares tests in the kit but is not an abstract generic named *Test. " +
                "A concrete one would run once, unparameterised, against no host at all")
            .ShouldHold();

    /// <summary>
    /// Every shared test base, is run by both suites.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#testing",
        "every shared test base is run by both hosts, or it is a test of one host wearing shared clothes")]
    public void EverySharedTestBase_IsRunByBothSuites()
    {
        SharedTestBases
            .Selected("shared test base")
            .SelectMany(shared => Solution.Suites
                .Where(suite => !IsDerivedIn(suite, shared))
                .Select(suite =>
                    $"{StripArity(shared.Name)} has no derivation in {suite.GetName().Name}. Wiring a " +
                    "shared base into one suite and not the other breaks nothing today, and proves " +
                    "half of what it claims to"))
            .ShouldHold();
    }

    /// <summary>
    /// Every derivation, declares its own collection.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#testing",
        "each derivation carries its own [Collection], because inherited collection attributes are not worth depending on")]
    public void EveryDerivation_DeclaresItsOwnCollection() =>
        Solution.Suites
            .SelectMany(suite => suite.DeclaredTypes())
            .Where(type => type is { IsAbstract: false, BaseType.IsGenericType: true })
            // Derived from a base that declares tests, not merely from a generic the kit exports.
            // Each suite's own ApiFactory derives from the kit's ApiFactory<TEntryPoint> and is not a
            // test class; asking it for a [Collection] would be asking the fixture to join itself.
            .Where(type => SharedTestBases.Contains(type.BaseType!.GetGenericTypeDefinition()))
            .Selected("derivation of a shared test base")
            .Where(type => !type.GetCustomAttributes<CollectionAttribute>(inherit: false).Any())
            .Select(type =>
                $"{type.FullName} declares no [Collection] of its own, so it depends on xUnit " +
                "discovering an inherited one")
            .ShouldHold();

    /// <summary>
    /// No fact, is asserted by both suites in their own code.
    /// </summary>
    /// <remarks>
    /// The three rules above look outward from the kit — a shared base is abstract and generic, is
    /// derived in both suites, and each derivation carries its own collection. Every one of them
    /// takes its population from types that are <em>already</em> in the kit, so the convention was
    /// checked in the direction where breaking it is impossible. A fact living in both suites and in
    /// neither the kit is invisible to all three, which is how twenty-one of them accumulated in
    /// <c>TrainerControllerTests</c> and <c>TrainingControllerTests</c> before anything noticed.
    /// <para>
    /// This is the other direction. A suite's own code is what it declares outside a derivation, and
    /// what belongs there is a fact about <em>that host</em> — the CQRS validation pipeline, and
    /// nothing else today. A fact asserted in both is a fact about the API, and the API is what the
    /// kit exists for: written once, run twice, so the two hosts cannot answer it differently
    /// without one of them going red.
    /// </para>
    /// <para>
    /// Matched by name, and that is a real limit rather than an oversight. A copy that was renamed
    /// on its way into the second suite escapes this — <c>GetById_Unknown_Returns404</c> and
    /// <c>GetById_NonExistent_Returns404</c> were the same fact and would not have been caught.
    /// What this stops is the copy that kept its name, which is how the duplication happens when
    /// somebody reaches for the file next door; catching the ones already here was never its job.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("README#testing",
        "a fact about the API is written once in the shared kit, so both suites run it rather than one")]
    public void NoFact_IsAssertedByBothSuitesInTheirOwnCode() =>
        Solution.Suites
            .SelectMany(suite => FactsDeclaredBy(suite)
                .Select(fact => (Suite: suite.GetName().Name!, fact.Type, fact.Name)))
            .Selected("fact a suite declares outside a derivation")
            .GroupBy(entry => entry.Name, StringComparer.Ordinal)
            .Where(group => group
                .Select(entry => entry.Suite)
                .Distinct(StringComparer.Ordinal)
                .Skip(1)
                .Any())
            .Select(group =>
                $"'{group.Key}' is asserted by both suites in their own code — " +
                $"{string.Join(" and ", group.Select(entry => entry.Type.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))}. " +
                "Two copies of one fact about the API drift apart silently, and the suite that was " +
                "not updated goes on passing. Move it to a *Test base in the kit and leave each " +
                "suite a derivation")
            .ShouldHold();

    /// <summary>
    /// The facts a suite declares itself, rather than inheriting from a base in the kit.
    /// </summary>
    /// <remarks>
    /// <see cref="BindingFlags.DeclaredOnly"/> is the whole of it, and it is deliberately *not*
    /// paired with a filter on the type. Excluding derivations of a shared base looked equivalent
    /// and was not: it hid a fact declared *inside* a derivation, which is the one way a suite can
    /// grow its own copy of a shared fact while still looking like a one-line file. The mistake was
    /// found by breaking this rule and watching it stay green, which is the only reason anybody
    /// finds a hole in a guard. A file that derives and adds nothing contributes nothing here,
    /// because inherited methods are not declared ones.
    /// </remarks>
    private static IEnumerable<(Type Type, string Name)> FactsDeclaredBy(Assembly suite) =>
        suite.DeclaredTypes()
            .Where(type => !type.IsAbstract)
            .SelectMany(type => type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<FactAttribute>(inherit: true).Any())
                .Select(method => (Type: type, method.Name)));

    /// <summary>The kit's abstract generic bases that actually declare tests.</summary>
    private static IReadOnlySet<Type> SharedTestBases { get; } =
        Solution.TestKit.DeclaredTypes()
            .Where(type => type is { IsAbstract: true, IsGenericTypeDefinition: true })
            .Where(DeclaresTests)
            .ToHashSet();

    private static bool DeclaresTests(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(method => method.GetCustomAttributes<FactAttribute>(inherit: true).Any());

    private static bool IsDerivedIn(Assembly suite, Type sharedBase) =>
        suite.DeclaredTypes().Any(type =>
            type is { IsAbstract: false, BaseType.IsGenericType: true }
            && type.BaseType!.GetGenericTypeDefinition() == sharedBase);

    private static string StripArity(string name) =>
        name.Contains('`', StringComparison.Ordinal)
            ? name[..name.IndexOf('`', StringComparison.Ordinal)]
            : name;
}
