using System.Reflection;
using TrainingHub.Architecture.Tests.Framework;

// Records this suite cannot defend, each with the reason.
//
// They live here rather than in the records themselves because a record's body is never rewritten
// once merged. Keeping the exemption on this side also puts it somewhere it can be checked, and it
// is: an excuse naming a record that does not exist fails, and so does an excuse for a record some
// rule has since started defending. An excuse cannot outlive its reason.
//
// It nearly did. Three entries stood here claiming that no type-level rule could see their
// decision, written when reflection was all this suite could do — and they outlived that by every
// rule since ADR 0026 that reads a source file or an MSBuild property. ADR 0039 replaced them with
// the rules they said were impossible. What is left is the one whose claims genuinely are not in
// the code at all.
//
// Records marked Superseded or Proposed need no entry. The coverage rule reads their own status
// line and leaves them alone, so a record superseded next year drops out of scope by itself.

[assembly: UnguardedRecord("0009",
    "The access token is held by the BFF and never reaches the browser. Every claim in that record is " +
    "about what happens over the wire: that the response to a sign-in carries a cookie and no token, " +
    "that a forged request is refused, that a call reaches the API with a credential the page never " +
    "held, that signing out ends the session rather than forgetting it. A rule could pin the cookie's " +
    "flags and the shape of the endpoints — and would prove none of those four, because each is about " +
    "what a request and its response contain. BffTests asserts them claim by claim, driving a real " +
    "host. (The earlier reason also said this suite does not reference the Blazor pair; it does, " +
    "through the BFF suite, and two rules read that host's Program.cs by path. That clause was " +
    "wrong and is gone.)")]

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
