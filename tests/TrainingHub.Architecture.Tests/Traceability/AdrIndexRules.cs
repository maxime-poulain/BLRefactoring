using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Traceability;

/// <summary>
/// The standing of a decision, written once and repeated faithfully.
/// </summary>
/// <remarks>
/// The coverage rules read two things from <c>docs/adr/</c>: the number in a record's heading and
/// the numbers in the index's links. The line that says whether a decision is still in force, and
/// what later records did to it, was parsed and compared to nothing — so thirteen of thirty-eight
/// records had drifted from their own row before anybody counted. See ADR 0039.
/// <para>
/// A record cites another as a link and the index cites it as a bare number, so both are flattened
/// before comparing: the two files may differ in how they spell a reference, and in nothing else.
/// </para>
/// </remarks>
public sealed partial class AdrIndexRules
{
    /// <summary>A citation of another record, as a record's own prose spells it.</summary>
    [GeneratedRegex(@"\[(?<number>\d{4})\]\([^)]+\)")]
    private static partial Regex RecordLink { get; }

    /// <summary>An index row: the record it links, and the cells that follow.</summary>
    [GeneratedRegex(@"^\|\s*\[(?<number>\d{4})\]\([^)]+\)\s*\|")]
    private static partial Regex IndexRow { get; }

    /// <summary>The field a record uses to name the record it amends.</summary>
    [GeneratedRegex(@"^-\s+\*\*Amends:\*\*\s+(?<targets>.+?)\s*$")]
    private static partial Regex AmendsField { get; }

    /// <summary>
    /// Every record's status, is repeated by the index.
    /// </summary>
    /// <remarks>
    /// Both directions in one comparison, since a difference is a difference: an index that
    /// annotates what the record does not say leaves the record's own reader uninformed, and an
    /// index that drops what the record says makes the table a worse summary than the file it
    /// summarises. Measured, not assumed: written against the drifted index — thirteen records,
    /// five of them annotated only in the table — and it failed on all thirteen before any of them
    /// was corrected.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0039",
        "a record's status is written once, in the record, and the index repeats it")]
    public void EveryRecordsStatus_IsRepeatedByTheIndex()
    {
        var listed = IndexStatuses();

        AdrCatalog.Records
            .Selected("record")
            .Where(record => !Flatten(record.Status).Equals(
                listed.GetValueOrDefault(record.Number, string.Empty), StringComparison.Ordinal))
            .Select(record => listed.TryGetValue(record.Number, out var cell)
                ? $"ADR {record.Number} says its status is '{Flatten(record.Status)}' and " +
                  $"docs/adr/README.md says '{cell}'. The record is the source; copy it into the index"
                : $"ADR {record.Number} has no row in docs/adr/README.md to carry its status")
            .ShouldHold();
    }

    /// <summary>
    /// Every amendment, is declared by both records.
    /// </summary>
    /// <remarks>
    /// An amendment declared on one side only is findable in one direction. A reader who opens the
    /// amended record — the one the code still cites, since its number never changes — has no way
    /// of learning that a later decision narrowed it. Three of the four amendments this repository
    /// declares were in exactly that state.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0039",
        "an amendment is declared on both sides: the amended record's status names the record that amended it")]
    public void EveryAmendment_IsDeclaredByBothRecords() =>
        Amendments()
            .Selected("declared amendment")
            .Where(amendment => AdrCatalog.Find(amendment.Target) is null
                || !Flatten(AdrCatalog.Find(amendment.Target)!.Status)
                    .Contains(amendment.Amender, StringComparison.Ordinal))
            .Select(amendment => AdrCatalog.Find(amendment.Target) is null
                ? $"ADR {amendment.Amender} amends {amendment.Target}, and no such record exists"
                : $"ADR {amendment.Amender} declares that it amends {amendment.Target}, and " +
                  $"{amendment.Target}'s status does not name it back. An amendment findable in one " +
                  "direction leaves the reader of the amended record uninformed")
            .ShouldHold();

    /// <summary>The status cell of every row in the index, by record number.</summary>
    private static IReadOnlyDictionary<string, string> IndexStatuses()
    {
        var statuses = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in SourceTree.ReadLines(AdrCatalog.IndexPath))
        {
            var row = IndexRow.Match(line);

            if (!row.Success)
            {
                continue;
            }

            var cells = line.Split('|');

            if (cells.Length > 3)
            {
                statuses[row.Groups["number"].Value] = Flatten(cells[3].Trim());
            }
        }

        if (statuses.Count == 0)
        {
            // Vacuity: a table whose shape changed would come back empty, and "every status is
            // repeated" would be true of nothing at all.
            throw new InvalidOperationException(
                "docs/adr/README.md holds no row of the form '| [NNNN](file.md) | Title | Status |'. " +
                "This rule reads that table to compare each record with its row; with none of them " +
                "found, it asserts nothing.");
        }

        return statuses;
    }

    /// <summary>The amendments the records declare, as (amender, target) pairs.</summary>
    private static IEnumerable<(string Amender, string Target)> Amendments() =>
        AdrCatalog.Records.SelectMany(record => SourceTree
            .ReadLines(Path.Combine(SourceTree.RepositoryRoot, record.RelativePath.Replace('/', Path.DirectorySeparatorChar)))
            .Select(line => AmendsField.Match(line))
            .Where(field => field.Success)
            .SelectMany(field => RecordLink.Matches(field.Groups["targets"].Value)
                .Select(link => link.Groups["number"].Value)
                .DefaultIfEmpty(field.Groups["targets"].Value.Trim()))
            .Select(target => (record.Number, target)));

    /// <summary>
    /// A status as it reads without markup: a cited record is its number, wherever it is cited.
    /// </summary>
    private static string Flatten(string status) =>
        RecordLink.Replace(status, match => match.Groups["number"].Value);
}
