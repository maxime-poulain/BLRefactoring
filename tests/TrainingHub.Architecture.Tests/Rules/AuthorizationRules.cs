using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// Where the answer to "who is allowed" is written down (ADR 0051).
/// </summary>
/// <remarks>
/// The administration is a second authority over the same model, not a second context, and the
/// whole weight of that decision rests on authorization staying at the boundary. The moment a use
/// case asks which role is calling, the difference stops being authorization and becomes a branch
/// in the model — and the application layer, which does not depend on the API, would have to grow a
/// notion of caller to support it.
/// </remarks>
public sealed class AuthorizationRules
{
    /// <summary>
    /// The boundaries: the two hosts, the shared API layer, the front end. These decide who may
    /// call what, and they are the only ones allowed to say so.
    /// </summary>
    private static readonly string[] BoundaryPrefixes =
    [
        "src/TrainingHub.Shared.Api/",
        "src/DDD/Api/",
        "src/DDDWithCqrs/Api/",
        "src/Web/"
    ];

    /// <summary>
    /// The vocabulary of authorization, in the forms an inner layer would reach for.
    /// </summary>
    /// <remarks>
    /// Narrow on purpose. <c>IdentityRoles.</c> carries its dot so that the framework's own
    /// <c>IdentityRole&lt;Guid&gt;</c> — which the identity <c>DbContext</c> legitimately names —
    /// is not mistaken for this repository's constant. The quoted literal catches the copy that
    /// bypasses the constant, which is the version somebody actually writes in a hurry.
    /// </remarks>
    private static readonly string[] RoleVocabulary =
    [
        "IdentityRoles.",
        "\"Administrator\"",
        "AdministratorPolicy",
        "ClaimTypes.Role",
        "RequireRole",
        "[Authorize"
    ];

    /// <summary>
    /// No inner layer, names a role.
    /// </summary>
    /// <remarks>
    /// Stated by exclusion rather than by listing the inner projects: a project added under
    /// <c>src/</c> tomorrow is inner until somebody says otherwise, which is the direction that
    /// fails safely. A list of inner layers would leave the new one unwatched and silent.
    /// <para>
    /// Read off the source, because the claim is about what a file <em>says</em>. A string literal
    /// leaves nothing in the metadata to reflect over, and the literal is the form this rule most
    /// needs to catch.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0051",
        "the application layer never names a role: it does not depend on the API, and a use case " +
        "that asked who is calling would have to")]
    public void NoInnerLayer_NamesARole() =>
        SourceTree.SourceFiles
            .Select(SourceTree.Relative)
            .Where(file => file.StartsWith("src/", StringComparison.Ordinal))
            .Where(file => !BoundaryPrefixes.Any(prefix => file.StartsWith(prefix, StringComparison.Ordinal)))
            .Where(file => !SourceTree.IsGenerated(Path.Combine(
                SourceTree.RepositoryRoot, file.Replace('/', Path.DirectorySeparatorChar))))
            .Selected("inner-layer source file")
            .SelectMany(file => RoleVocabulary
                .Where(term => SourceTree
                    .ReadText(Path.Combine(
                        SourceTree.RepositoryRoot, file.Replace('/', Path.DirectorySeparatorChar)))
                    .Contains(term, StringComparison.Ordinal))
                .Select(term =>
                    $"'{file}' names '{term}'. Who may call a use case is decided at the API, by a " +
                    "policy, and nowhere else — an inner layer that knows about roles is an " +
                    "administration turning into a model of its own (ADR 0051)"))
            .ShouldHold();
}
