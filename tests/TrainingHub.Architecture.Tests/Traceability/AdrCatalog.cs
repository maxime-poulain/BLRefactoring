using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;

namespace TrainingHub.Architecture.Tests.Traceability;

/// <summary>One record, as the file declares itself.</summary>
internal sealed record AdrRecord(string Number, string Title, string Status, string RelativePath)
{
    /// <summary>
    /// Whether this record still describes the architecture, and therefore still needs defending.
    /// </summary>
    /// <remarks>
    /// A superseded record is history: its successor carries the decision, and the successor is the
    /// one a rule must name. A proposed record is not a decision yet. Both drop out of scope from
    /// their own status line, without anybody maintaining a second list.
    /// </remarks>
    public bool IsInForce =>
        !Status.StartsWith("Superseded", StringComparison.OrdinalIgnoreCase)
        && !Status.StartsWith("Proposed", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The records under <c>docs/adr/</c>, read rather than restated.
/// </summary>
/// <remarks>
/// A list of record numbers written into this suite would be wrong the day someone adds a record —
/// and wrong in the direction that passes, since a record nobody listed is a record nobody checks.
/// Reading the directory is the only version of this that cannot go quietly stale.
/// </remarks>
internal static partial class AdrCatalog
{
    [GeneratedRegex(@"^#\s+(?<number>\d{4})\s+—\s+(?<title>.+?)\s*$")]
    private static partial Regex Heading { get; }

    [GeneratedRegex(@"^-\s+\*\*Status:\*\*\s+(?<status>.+?)\s*$")]
    private static partial Regex Status { get; }

    public static IReadOnlyList<AdrRecord> Records { get; } = Read();

    /// <summary>The index that lists them, for the rule that no record is missing from it.</summary>
    public static string IndexPath { get; } = Path.Combine(SourceTree.RepositoryRoot, "docs", "adr", "README.md");

    public static AdrRecord? Find(string number) =>
        Records.SingleOrDefault(record => record.Number == number);

    private static IReadOnlyList<AdrRecord> Read()
    {
        var directory = Path.Combine(SourceTree.RepositoryRoot, "docs", "adr");

        var records = Directory
            .EnumerateFiles(directory, "*.md")
            .Where(path => !Path.GetFileName(path).Equals("README.md", StringComparison.Ordinal))
            .Select(Parse)
            .OrderBy(record => record.Number, StringComparer.Ordinal)
            .ToArray();

        if (records.Length == 0)
        {
            // Vacuity again: a catalogue that found nothing makes every coverage rule pass forever.
            throw new InvalidOperationException(
                $"No architecture decision record was found under '{directory}'. The coverage rules " +
                "answer to these files; with none of them read, they assert nothing.");
        }

        return records;
    }

    private static AdrRecord Parse(string path)
    {
        var lines = SourceTree.ReadLines(path);

        var heading = lines
            .Select(line => Heading.Match(line))
            .FirstOrDefault(match => match.Success)
            ?? throw new InvalidOperationException(
                $"'{SourceTree.Relative(path)}' opens with no '# NNNN — Title' heading. " +
                "docs/adr/README.md makes that line the record's identity.");

        var status = lines
            .Select(line => Status.Match(line))
            .FirstOrDefault(match => match.Success)
            ?? throw new InvalidOperationException(
                $"'{SourceTree.Relative(path)}' declares no '- **Status:**' line. Status is one of " +
                "Proposed, Accepted or Superseded by NNNN, and the coverage rules read it to know " +
                "whether the record still needs defending.");

        return new AdrRecord(
            heading.Groups["number"].Value,
            heading.Groups["title"].Value,
            status.Groups["status"].Value,
            SourceTree.Relative(path));
    }
}
