using System.Runtime.CompilerServices;
using AwesomeAssertions;
using NetArchTest.Rules;

namespace BLRefactoring.Architecture.Tests.Framework;

/// <summary>
/// Turns a rule's outcome into a failure a reader can act on without opening this project.
/// </summary>
/// <remarks>
/// <c>result.IsSuccessful.Should().BeTrue()</c> prints "Expected result.IsSuccessful to be true, but
/// found False", which names neither the offender nor the decision. A failure has to say four
/// things: which types broke the rule, why, where they are declared, and which record says they may
/// not. The first three come from the library; the fourth comes from the
/// <see cref="ArchitectureRuleAttribute"/> on the calling method, found through
/// <see cref="CallerMemberNameAttribute"/> — so a rule names its record once and never restates it.
/// <para>
/// Both overloads word failures identically on purpose. Half of this suite cannot be expressed in
/// NetArchTest and is written by hand against reflection or the file tree; a reader should not be
/// able to tell which half a failure came from.
/// </para>
/// </remarks>
internal static class RuleAssertions
{
    /// <summary>Asserts a NetArchTest rule, and that it had something to assert on.</summary>
    /// <remarks>
    /// The vacuity check comes first, and it is the assertion that matters most over a decade. A
    /// predicate selecting nothing satisfies every condition placed after it: rename a namespace,
    /// move a folder, drop a suffix, and the rule stops matching anything and goes on passing
    /// forever. That is how an architecture suite rots, and it rots green. Asserted before the rule
    /// itself so the failure reads "selected no type" rather than "no violations".
    /// </remarks>
    public static void ShouldHold(this TestResult result, [CallerMemberName] string rule = "")
    {
        result.SelectedTypesForTesting.Should().NotBeEmpty(
            "{0}",
            RuleIndex.Because(rule, "this rule matched no type at all, so it proves nothing"));

        if (result.IsSuccessful)
        {
            return;
        }

        result.FailingTypes.Select(Describe).ShouldHold(rule);
    }

    /// <summary>The same failure shape for the rules written by hand.</summary>
    /// <remarks>
    /// Takes the violations already rendered as sentences: a hand-rolled rule knows better than any
    /// helper how to describe what it found. Guard the population with <see cref="Selected{T}"/>
    /// before filtering, or this asserts nothing for the same reason as above.
    /// <para>
    /// Rendered into one string rather than asserted as a collection, which is not cosmetic.
    /// <c>BeEmpty</c> on a sequence reports "found at least one item" and prints that one item, so a
    /// rule catching twelve offenders takes twelve runs to read — and each run only reveals the next
    /// one. A rule that fires names everything it found.
    /// </para>
    /// </remarks>
    public static void ShouldHold(this IEnumerable<string> found, [CallerMemberName] string rule = "")
    {
        var violations = string.Concat(
            found.Select(violation => $"{Environment.NewLine}  - {violation}"));

        violations.Should().BeEmpty("{0}", RuleIndex.Because(rule));
    }

    /// <summary>
    /// The population a hand-rolled rule is about, asserted to be non-empty before it is filtered.
    /// </summary>
    /// <remarks>
    /// The reflection half of this suite needs the same protection <see cref="ShouldHold(TestResult, string)"/>
    /// gives the other half. "No aggregate root has a public constructor" is satisfied trivially by
    /// finding no aggregate root, and that is exactly what a rename would cause.
    /// </remarks>
    /// <param name="population">The types, files or members the rule is about.</param>
    /// <param name="what">What they are, in the plural, for the failure message: "aggregate root".</param>
    /// <param name="rule">Supplied by the compiler; never pass this.</param>
    public static IReadOnlyList<T> Selected<T>(
        this IEnumerable<T> population,
        string what,
        [CallerMemberName] string rule = "")
    {
        var selected = population.ToArray();

        selected.Should().NotBeEmpty(
            "{0}",
            RuleIndex.Because(rule, $"this rule found no {what} to check, so it proves nothing"));

        return selected;
    }

    /// <summary>A type, its offence and where to find it.</summary>
    /// <remarks>
    /// <c>Explanation</c> is the enhanced fork's account of why the dependency search failed —
    /// "depends on Microsoft.EntityFrameworkCore" rather than a bare name. <c>SourceFilePath</c>
    /// comes from the PDB and is null when none was loaded, so the message degrades rather than
    /// throwing.
    /// </remarks>
    private static string Describe(IType type)
    {
        var why = string.IsNullOrWhiteSpace(type.Explanation)
            ? string.Empty
            : $" — {type.Explanation}";

        var where = string.IsNullOrWhiteSpace(type.SourceFilePath)
            ? string.Empty
            : $" ({SourceTree.Relative(type.SourceFilePath)})";

        return $"{type.FullName}{why}{where}";
    }
}
