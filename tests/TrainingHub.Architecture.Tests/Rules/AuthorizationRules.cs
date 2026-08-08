using System.Reflection;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
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

    /// <summary>
    /// The policies that demand a trainer, and which an administrator therefore cannot satisfy.
    /// </summary>
    private static readonly string[] TrainerBoundPolicies =
    [
        TrainerPolicy.Name,
        TrainingOwnerPolicy.Name
    ];

    /// <summary>
    /// No action, is behind both authorities at once.
    /// </summary>
    /// <remarks>
    /// The trap ADR 0054 exists to close, and it is silent: a policy declared on an action does not
    /// replace its controller's, it is added to it. Writing
    /// <c>[Authorize(Policy = AdministratorPolicy.Name)]</c> on an action of a controller deriving
    /// from <c>ApiControllerBase</c> compiles, routes, publishes a perfectly ordinary operation, and
    /// answers <c>403</c> to every caller in the system — the administrator for want of a
    /// <c>trainer_id</c>, the trainer for want of the role. Nothing fails at start-up, and the only
    /// symptom is a refusal that looks exactly like a correct one.
    /// <para>
    /// Read off the metadata rather than the source, because inheritance is the whole point: the
    /// offending action would carry one attribute and inherit the other, and neither file would look
    /// wrong on its own.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0054",
        "an administrative action lives on the administrative base, because a policy on an action " +
        "is added to its controller's rather than replacing it")]
    public void NoAction_IsBehindBothAuthoritiesAtOnce() =>
        Solution.Hosts
            .SelectMany(host => host.DeclaredTypes())
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(action => action.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .Select(action => (Controller: controller, Action: action)))
            .Selected("action")
            .Select(entry => (entry.Controller, entry.Action, Policies: PoliciesInForce(entry.Controller, entry.Action)))
            .Where(entry => entry.Policies.Contains(AdministratorPolicy.Name)
                            && entry.Policies.Overlaps(TrainerBoundPolicies))
            .Select(entry =>
                $"{entry.Controller.Name}.{entry.Action.Name} is behind {string.Join(" and ", entry.Policies)}. " +
                "Those are combined, not chosen between, so the action is reachable by nobody: an " +
                "administrator carries no trainer_id and a trainer carries no role. Move it to a " +
                "controller deriving from AdministrationControllerBase (ADR 0054)")
            .ShouldHold();

    /// <summary>
    /// Every policy an action is held to: its own, and every one it inherits from its controller.
    /// </summary>
    private static IReadOnlySet<string> PoliciesInForce(Type controller, MethodInfo action) =>
        controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(action.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .Select(attribute => attribute.Policy)
            .Where(policy => !string.IsNullOrEmpty(policy))
            .ToHashSet(StringComparer.Ordinal)!;
}
