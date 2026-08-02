using System.Text.RegularExpressions;
using BLRefactoring.Architecture.Tests.Framework;
using Xunit;

namespace BLRefactoring.Architecture.Tests.Traceability;

/// <summary>
/// The rule about the rules: every decision still in force is defended by one, or says why it cannot be.
/// </summary>
/// <remarks>
/// This is what makes the records executable rather than decorative. Without it the suite protects
/// whatever somebody happened to write a test for, and a record added next year arrives with no
/// rule, no failure, and nothing to say it is unguarded — which is the state this repository was in
/// before, one level up.
/// </remarks>
public sealed class AdrCoverageRules
{
    private static readonly Regex IndexRow = new(@"\[(?<number>\d{4})\]\((?<file>[^)]+)\)", RegexOptions.Compiled);

    private static IReadOnlySet<string> Excused { get; } =
        UnguardedRecords.All.Select(excuse => excuse.Record).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Every record in force, is defended by a rule, or says why it cannot be.
    /// </summary>
    [Fact]
    [ArchitectureRule("0013",
        "a decision nothing keeps true has already been half reversed")]
    public void EveryRecordInForce_IsDefendedByARule_OrSaysWhyItCannotBe() =>
        AdrCatalog.Records
            .Where(record => record.IsInForce)
            .Selected("record still in force")
            .Where(record => !RuleIndex.Records.Contains(record.Number) && !Excused.Contains(record.Number))
            .Select(record =>
                $"ADR {record.Number} — {record.Title} ({record.RelativePath}) is defended by no rule " +
                "and carries no entry in UnguardedRecords. Either write the rule, or write down why " +
                "there cannot be one")
            .ShouldHold();

    /// <summary>
    /// No record, is both defended and excused.
    /// </summary>
    [Fact]
    [ArchitectureRule("0013",
        "an excuse that outlives its reason is worse than no excuse at all")]
    public void NoRecord_IsBothDefendedAndExcused() =>
        UnguardedRecords.All
            .Selected("entry in the ledger of unguarded records")
            .Where(excuse => RuleIndex.Records.Contains(excuse.Record))
            .Select(excuse =>
                $"ADR {excuse.Record} is defended by a rule and still listed as unguardable. Delete " +
                "the entry: it now claims something false about the suite")
            .ShouldHold();

    /// <summary>
    /// No excuse, names a record that does not exist.
    /// </summary>
    [Fact]
    [ArchitectureRule("0013",
        "the ledger names records, not numbers somebody remembered")]
    public void NoExcuse_NamesARecordThatDoesNotExist() =>
        UnguardedRecords.All
            .Selected("entry in the ledger of unguarded records")
            .Where(excuse => AdrCatalog.Find(excuse.Record) is null)
            .Select(excuse => $"the ledger excuses ADR {excuse.Record}, and no such record exists")
            .ShouldHold();

    /// <summary>
    /// Every rule names something that exists.
    /// </summary>
    [Fact]
    [ArchitectureRule("0013",
        "every rule answers to something: a record that exists, or a section of the README")]
    public void EveryRuleNamesSomethingThatExists() =>
        RuleIndex.Rules
            .Selected("rule")
            .Where(rule => !rule.Declaration.Record.StartsWith("README#", StringComparison.Ordinal))
            .Where(rule => AdrCatalog.Find(rule.Declaration.Record) is null)
            .Select(rule =>
                $"{rule.Name} defends ADR {rule.Declaration.Record}, and no record carries that number")
            .ShouldHold();

    /// <summary>
    /// Every record, is listed in the index.
    /// </summary>
    [Fact]
    [ArchitectureRule("0013",
        "the index lists every record, since the index is how a reader finds one")]
    public void EveryRecord_IsListedInTheIndex()
    {
        var listed = SourceTree.ReadLines(AdrCatalog.IndexPath)
            .SelectMany(line => IndexRow.Matches(line).Select(match => match.Groups["number"].Value))
            .ToHashSet(StringComparer.Ordinal);

        AdrCatalog.Records
            .Selected("record")
            .Where(record => !listed.Contains(record.Number))
            .Select(record => $"ADR {record.Number} — {record.Title} is in no row of docs/adr/README.md")
            .Concat(listed
                .Where(number => AdrCatalog.Find(number) is null)
                .Select(number => $"docs/adr/README.md lists {number}, and no such record exists"))
            .ShouldHold();
    }
}
