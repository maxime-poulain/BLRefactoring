using System.Reflection;
using TrainingHub.Architecture.Tests.Traceability;
using Xunit;

namespace TrainingHub.Architecture.Tests.Framework;

/// <summary>A rule of this suite, and the decision it answers to.</summary>
internal sealed record DeclaredRule(MethodInfo Method, ArchitectureRuleAttribute Declaration)
{
    public string Name => $"{Method.DeclaringType!.FullName}.{Method.Name}";
}

/// <summary>
/// This suite, read back by reflection: which rules exist, and what each of them defends.
/// </summary>
/// <remarks>
/// Two readers, one source. The coverage rules ask which records are named, so that a record named
/// by nothing fails. <see cref="RuleAssertions"/> asks what a failing rule was defending, so that a
/// violation quotes the decision instead of printing <c>false</c>. Because both read the attribute
/// on the rule itself, the record number and the sentence are each written exactly once — at the
/// test that enforces them, which is the only place that cannot drift from them.
/// </remarks>
internal static class RuleIndex
{
    /// <summary>Every <c>[Fact]</c> and <c>[Theory]</c> in this assembly, attributed or not.</summary>
    public static IReadOnlyList<MethodInfo> AllTests { get; } =
    [
        .. typeof(RuleIndex).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttributes<FactAttribute>(inherit: true).Any())
            .OrderBy(method => method.Name, StringComparer.Ordinal)
    ];

    public static IReadOnlyList<DeclaredRule> Rules { get; } =
    [
        .. AllTests.SelectMany(method => method
            .GetCustomAttributes<ArchitectureRuleAttribute>()
            .Select(declaration => new DeclaredRule(method, declaration)))
    ];

    /// <summary>The records and README sections this suite claims to defend.</summary>
    public static IReadOnlySet<string> Records { get; } =
        Rules.Select(rule => rule.Declaration.Record).ToHashSet(StringComparer.Ordinal);

    private static readonly ILookup<string, DeclaredRule> ByMethodName =
        Rules.ToLookup(rule => rule.Method.Name, StringComparer.Ordinal);

    /// <summary>
    /// The "because" clause of a failure: what the rule defends, and where that is written down.
    /// </summary>
    /// <remarks>
    /// Takes a method name rather than the attribute, so a rule states its record once — in the
    /// attribute — and never again in the assertion. The caller's name arrives through
    /// <see cref="System.Runtime.CompilerServices.CallerMemberNameAttribute"/>.
    /// <para>
    /// A method with no attribute still gets a usable sentence rather than an exception: failing
    /// here would replace a real violation with a puzzle about this class. <c>SuiteSelfRules</c> is
    /// what fails on an unattributed rule, once, by name.
    /// </para>
    /// </remarks>
    public static string Because(string method, string? detail = null)
    {
        var rule = ByMethodName[method].FirstOrDefault();

        if (rule is null)
        {
            return detail is null
                ? $"'{method}' declares no [ArchitectureRule], so it names no decision"
                : $"'{method}' declares no [ArchitectureRule], so it names no decision — and {detail}";
        }

        var record = AdrCatalog.Find(rule.Declaration.Record);

        var source = record is null
            ? rule.Declaration.Record
            : $"ADR {record.Number} — {record.Title} ({record.RelativePath})";

        return detail is null
            ? $"{source} says: {rule.Declaration.Rule}"
            : $"{source} says: {rule.Declaration.Rule} — and {detail}";
    }
}
