using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What separates a lifecycle from a soft delete wearing an enum (ADR 0050).
/// </summary>
/// <remarks>
/// A status field is cheap to add and cheap to get wrong. The wrong version is a column the write
/// side respects and every reader ignores: nothing is announced, so no read model, no index and no
/// other context ever learns the state changed, and what was sold as a lifecycle turns out to be a
/// tombstone with two values. The record's whole argument rests on the difference, so the
/// difference is what a rule has to hold.
/// </remarks>
public sealed partial class LifecycleRules
{
    // A status changing hands: `Status = something;` as a statement, at the start of a line. The
    // property's own declaration does not match — there the line begins with its modifiers and
    // type, and the initialiser sits after `{ get; private set; }`.
    [GeneratedRegex(@"^\s*Status\s*=\s*[^=]", RegexOptions.Multiline)]
    private static partial Regex StatusAssignment { get; }

    // Where a member begins: four spaces, then an access modifier. File-scoped namespaces and
    // Allman braces make that exact throughout this domain, which is what lets a file be split
    // into members without parsing it.
    [GeneratedRegex(@"^    (?=(public|private|protected|internal)\s)", RegexOptions.Multiline)]
    private static partial Regex MemberBoundary { get; }

    // Staging an aggregate for removal: `something.Delete(` on a repository. The repository's own
    // declaration of the method has no receiver before it, and its implementation calls EF's
    // `Remove`, so neither matches — what matches is a use case asking for a deletion.
    [GeneratedRegex(@"\.Delete\(")]
    private static partial Regex RepositoryDeletion { get; }

    private static IEnumerable<string> DomainSourceFiles =>
        SourceTree.SourceFiles
            .Select(SourceTree.Relative)
            .Where(file => file.StartsWith("src/TrainingHub.Shared.Domain/", StringComparison.Ordinal));

    private static IEnumerable<string> ProductionSourceFiles =>
        SourceTree.SourceFiles
            .Select(SourceTree.Relative)
            .Where(file => file.StartsWith("src/", StringComparison.Ordinal));

    private static IEnumerable<string> MembersOf(string file) =>
        MemberBoundary.Split(SourceTree.ReadText(
            Path.Combine(SourceTree.RepositoryRoot, file.Replace('/', Path.DirectorySeparatorChar))));

    /// <summary>
    /// Every status transition, announces itself.
    /// </summary>
    /// <remarks>
    /// Read off the source rather than the metadata, because the claim is about what a method
    /// <em>does</em> and no reflection can see that. The unit is a member: a member that moves a
    /// status and never calls <c>AddDomainEvent</c> is the defect, wherever in its body the two
    /// happen to sit.
    /// <para>
    /// The rule deliberately does not check that the fact matches the state — a method could raise
    /// the wrong event and still pass here. That is the unit tests' job, and they do it. What this
    /// holds is the part a unit test would not notice was missing: a transition nobody wrote a test
    /// for at all still cannot be silent.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0050",
        "every state transition raises a fact, which is the claim that separates this from a soft delete")]
    public void EveryStatusTransition_AnnouncesItself() =>
        DomainSourceFiles
            .Selected("domain source file")
            .SelectMany(file => MembersOf(file)
                .Where(member => StatusAssignment.IsMatch(member))
                .Where(member => !member.Contains("AddDomainEvent", StringComparison.Ordinal))
                .Select(member =>
                    $"'{file}' moves a status in a member that raises no domain event: " +
                    $"'{FirstLineOf(member)}'. A state nothing announces is a state no reader ever " +
                    "learns about — an index keeps serving what was withdrawn, and the lifecycle is " +
                    "a soft delete after all (ADR 0050)"))
            .ShouldHold();

    /// <summary>
    /// Every deletion, announces itself.
    /// </summary>
    /// <remarks>
    /// The sibling above watches members that <em>move</em> a status, and a deletion moves none — it
    /// removes the row the status was written on. That is not a gap in how the rule is written but
    /// in what it is about, and it is the gap the trainer-deletion cascade fell into: it removed
    /// every training a departing trainer owned and announced not one of them, so the search index
    /// went on serving all of them. The two halves of ADR 0050 need two rules — one for the
    /// transitions, one for the way out.
    /// <para>
    /// Deliberately not restricted to trainings. No use case deletes a trainer today, the endpoint
    /// having been withdrawn until a role is entitled to it; when one returns it will stage a
    /// deletion through a repository like every other, and this rule will already be waiting for it.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0050",
        "deleting a training announces it, which is what the deletion never used to do")]
    public void EveryDeletion_AnnouncesItself() =>
        ProductionSourceFiles
            .Selected("production source file")
            .SelectMany(file => MembersOf(file)
                .Where(member => RepositoryDeletion.IsMatch(member))
                .Where(member => !member.Contains("MarkForDeletion", StringComparison.Ordinal))
                .Select(member =>
                    $"'{file}' stages a deletion in a member that announces nothing: " +
                    $"'{FirstLineOf(member)}'. Rows leave the database and every reader built from " +
                    "them keeps serving what is gone — the search index first of all, which is the " +
                    "defect ADR 0050 exists to close"))
            .ShouldHold();

    /// <summary>
    /// The states that owe an explanation, each with the property that carries it.
    /// </summary>
    /// <remarks>
    /// Two pairs today, and the shape rather than the list is what this rule is about: a third
    /// sanction added tomorrow states itself here and is watched from its first line.
    /// </remarks>
    private static readonly (string State, string Reason)[] ReasonedStates =
    [
        ("Status = TrainingStatus.Withheld", "WithholdingReason"),
        ("Status = TrainerStatus.Suspended", "SuspensionReason"),
    ];

    /// <summary>
    /// Every reasoned state, is written with its reason.
    /// </summary>
    /// <remarks>
    /// ADR 0052 states its invariant in both directions — the reason is present <em>if and only
    /// if</em> the state is the one it motivates — and both directions are read here, because each
    /// rots differently.
    /// <para>
    /// A member that enters the state without writing the reason leaves a <em>mute sanction</em>:
    /// the training is unavailable and its owner is told nothing, which is the one thing the record
    /// exists to prevent. A member that writes a reason without moving the status leaves an
    /// <em>orphan reason</em>: a row explaining a decision nobody took.
    /// </para>
    /// <para>
    /// Neither is a compile error and neither would fail a unit test that only checked the state, so
    /// the pair is held here, by shape, at the one place both halves are visible at once — the
    /// member that does the writing.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0052",
        "the reason is present if and only if the state is the one it motivates, which is what " +
        "forbids an orphan reason and a mute state alike")]
    public void EveryReasonedState_IsWrittenWithItsReason() =>
        DomainSourceFiles
            .Selected("domain source file")
            .SelectMany(file => MembersOf(file).SelectMany(member => ReasonedStates
                .Select(pair => Violation(file, member, pair))
                .Where(violation => violation is not null)
                .Select(violation => violation!)))
            .ShouldHold();

    private static string? Violation(string file, string member, (string State, string Reason) pair)
    {
        var entersTheState = member.Contains(pair.State, StringComparison.Ordinal);
        var writesTheReason = member.Contains($"{pair.Reason} =", StringComparison.Ordinal);

        if (entersTheState && !writesTheReason)
        {
            return $"'{file}' enters '{pair.State}' in a member that never writes {pair.Reason}: " +
                   $"'{FirstLineOf(member)}'. A state that owes an explanation and carries none is " +
                   "a sanction the product cannot explain to the person it lands on (ADR 0052)";
        }

        if (writesTheReason && !StatusAssignment.IsMatch(member))
        {
            return $"'{file}' writes {pair.Reason} in a member that moves no status: " +
                   $"'{FirstLineOf(member)}'. A reason that travels without its state is a row " +
                   "explaining a decision nobody took (ADR 0052)";
        }

        return null;
    }

    private static string FirstLineOf(string member) =>
        member.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? member;
}
