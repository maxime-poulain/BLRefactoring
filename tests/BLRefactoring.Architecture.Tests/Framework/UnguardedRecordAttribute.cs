namespace BLRefactoring.Architecture.Tests.Framework;

/// <summary>
/// Declares a record that no rule in this suite can defend, and says why.
/// </summary>
/// <remarks>
/// Applied to the assembly rather than written into the record itself. <c>docs/adr/README.md</c>
/// says a record is never rewritten once merged, and every record this excuses was merged before
/// this suite existed — so the excuse lives on the side that can be checked.
/// <para>
/// And it is checked, in both directions: an excuse naming a record that does not exist fails, and
/// so does an excuse for a record some rule has since started defending. An excuse cannot outlive
/// its reason.
/// </para>
/// <para>
/// Records marked <c>Superseded</c> or <c>Proposed</c> need no entry — the coverage rule reads
/// their own status line and leaves them alone.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class UnguardedRecordAttribute(string record, string reason) : Attribute
{
    public string Record { get; } = record;

    public string Reason { get; } = reason;
}
