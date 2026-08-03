using System.Reflection;
using TrainingHub.Architecture.Tests.Framework;

// Records this suite cannot defend, each with the reason.
//
// They live here rather than in the records themselves because docs/adr/README.md says a record is
// never rewritten once merged, and every one of these was merged before this suite existed. Keeping
// the exemption on this side also puts it somewhere it can be checked, and it is: an excuse naming
// a record that does not exist fails, and so does an excuse for a record some rule has since started
// defending. An excuse cannot outlive its reason.
//
// Records marked Superseded or Proposed need no entry. The coverage rule reads their own status
// line and leaves them alone, so a record superseded next year drops out of scope by itself.

[assembly: UnguardedRecord("0003",
    "Migrations are applied on startup in Development only. What decides that is a runtime branch on " +
    "IHostEnvironment inside DatabaseMigrationExtensions: a type-level rule can see the branch exists " +
    "but not which way it goes. The integration suites run the host in Development and exercise the " +
    "path that matters.")]

[assembly: UnguardedRecord("0005",
    "Audit timestamps are stored at full precision. The decision lives in a column type and an " +
    "interceptor's clock reading, and what makes it true is that a value survives the round trip — " +
    "which is a question for a database, not for metadata. TimestampPrecisionTest asks it of a real " +
    "SQL Server, on both hosts.")]

[assembly: UnguardedRecord("0009",
    "The access token is held by the BFF and never reaches the browser. Every claim in that record is " +
    "about what happens over the wire: that a cookie carries no token, that a forged request is " +
    "refused, that signing out ends the session rather than forgetting it. None of it is visible in a " +
    "type, and the Blazor pair is a net9.0 island this suite does not reference. BffTests asserts it, " +
    "claim by claim.")]

namespace TrainingHub.Architecture.Tests.Traceability;

/// <summary>
/// Anchors the assembly-level attributes above, which need a file and no type of their own.
/// </summary>
internal static class UnguardedRecords
{
    public static IReadOnlyList<UnguardedRecordAttribute> All { get; } =
    [
        .. typeof(UnguardedRecords).Assembly.GetCustomAttributes<UnguardedRecordAttribute>()
    ];
}
