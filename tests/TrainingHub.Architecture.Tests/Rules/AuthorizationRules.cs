using System.Reflection;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Api.Authorization;
using TrainingHub.Shared.Api.Identity;
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
    /// The verbs that change something. Everything else is a read, and a suspended trainer keeps it.
    /// </summary>
    private static readonly string[] WritingVerbs = ["POST", "PUT", "PATCH", "DELETE"];

    /// <summary>
    /// Every write of the trainer surface, is refused to a suspended trainer.
    /// </summary>
    /// <remarks>
    /// The rule ADR 0053 needs in order to stay true after the commit that builds it. Its decision
    /// is a table — every read kept, every write refused — and a table is exactly the kind of claim
    /// that decays one endpoint at a time: the next write added to the trainer surface will be
    /// written by somebody reading a neighboring action, and the neighbor that gets copied is
    /// whichever one they opened first.
    /// <para>
    /// Stated over the verb rather than over a list of route names, so that an endpoint added
    /// tomorrow is covered the day it appears rather than the day somebody remembers this rule.
    /// Read off the metadata for the same reason <c>NoAction_IsBehindBothAuthoritiesAtOnce</c> is:
    /// the policy a write is held to is partly inherited, and neither file looks wrong alone.
    /// </para>
    /// <para>
    /// The administrative surface is excluded because it is not the trainer's: an administrator is
    /// nobody's trainer, carries no standing, and suspending one is not a thing this product can do.
    /// </para>
    /// <para>
    /// One write is exempted by name, and the exemption is the amendment rather than a hole:
    /// ADR 0085 amends ADR 0053 so that erasing the account is the one write a suspension does not
    /// take away — the right to leave outlives the sanction, and the withheld trainings die with
    /// the account. The action sits behind <c>TrainerPolicy</c> deliberately, and pinning it here
    /// is what keeps the next reader from "fixing" it into the refusal the record argues against.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0053",
        "a suspended trainer keeps every read and loses every write, and the refusal is at the " +
        "boundary rather than in the domain")]
    public void EveryWriteOfTheTrainerSurface_IsRefusedToASuspendedTrainer() =>
        Solution.Hosts
            .SelectMany(host => host.DeclaredTypes())
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(action => (Controller: controller, Action: action, Verbs: Verbs(action)))
                .Where(entry => entry.Verbs.Overlaps(WritingVerbs)))
            .Selected("writing action")
            .Where(entry => entry.Action.Name != TheOneWriteASuspensionKeeps)
            .Select(entry => (entry.Controller, entry.Action, Policies: PoliciesInForce(entry.Controller, entry.Action)))
            .Where(entry => entry.Policies.Contains(TrainerPolicy.Name)
                            && !entry.Policies.Contains(ActiveTrainerPolicy.Name))
            .Select(entry =>
                $"{entry.Controller.Name}.{entry.Action.Name} writes on the trainer surface and is " +
                $"not behind {ActiveTrainerPolicy.Name}. A suspended trainer would reach it, and the " +
                "refusal ADR 0053 puts at the boundary would exist for every other write but this one")
            .ShouldHold();

    /// <summary>
    /// The action ADR 0085 exempts from ADR 0053's table: erasing the account is the one write a
    /// suspension does not take away.
    /// </summary>
    private const string TheOneWriteASuspensionKeeps = "EraseAccount";

    /// <summary>
    /// No read, and no administrative action, is behind the standing policy.
    /// </summary>
    /// <remarks>
    /// The other half of ADR 0053's sentence, and the half that is easy to lose by being helpful.
    /// Moving the policy onto <c>ApiControllerBase</c> would guard every write in one line and take
    /// the suspended trainer's own profile and catalog away from them at the same time — the
    /// alternative that record rejected by name, because "their trainings exist, they are theirs,
    /// and hiding them from their owner serves nobody".
    /// <para>
    /// The administrative surface is refused it for a different reason: an administrator is nobody's
    /// trainer and carries no standing, so the policy would be asking a question about them that has
    /// no answer.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0053",
        "keeping the reads is not a softening of the sanction; it is what makes the sanction " +
        "accountable")]
    public void NoReadOrAdministrativeAction_IsBehindTheStandingPolicy() =>
        Solution.Hosts
            .SelectMany(host => host.DeclaredTypes())
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(action => (Controller: controller, Action: action, Verbs: Verbs(action)))
                .Where(entry => entry.Verbs.Count > 0))
            .Selected("routed action")
            .Select(entry => (entry.Controller, entry.Action, entry.Verbs,
                Policies: PoliciesInForce(entry.Controller, entry.Action)))
            .Where(entry => entry.Policies.Contains(ActiveTrainerPolicy.Name))
            .Where(entry => !entry.Verbs.Overlaps(WritingVerbs)
                            || entry.Policies.Contains(AdministratorPolicy.Name))
            .Select(entry =>
                $"{entry.Controller.Name}.{entry.Action.Name} is behind {ActiveTrainerPolicy.Name} " +
                "and should not be: a suspended trainer keeps every read, and an administrator has " +
                "no standing for the policy to ask about (ADR 0053)")
            .ShouldHold();

    /// <summary>Where the browser's own vocabulary is written, and the guarded page families.</summary>
    private const string SessionClaimsFile =
        "src/Web/TrainingHub.Blazor/TrainingHub.Blazor.Client/Authorization/SessionClaims.cs";

    private const string SessionPoliciesFile =
        "src/Web/TrainingHub.Blazor/TrainingHub.Blazor.Client/Authorization/SessionPolicies.cs";

    private const string SessionRolesFile =
        "src/Web/TrainingHub.Blazor/TrainingHub.Blazor.Client/Authorization/SessionRoles.cs";

    private const string TrainerPages =
        "src/Web/TrainingHub.Blazor/TrainingHub.Blazor.Client/Pages/Trainings/";

    private const string ProfilePages =
        "src/Web/TrainingHub.Blazor/TrainingHub.Blazor.Client/Pages/Profile/";

    private const string AdministrationPages =
        "src/Web/TrainingHub.Blazor/TrainingHub.Blazor.Client/Pages/Administration/";

    /// <summary>
    /// The browser's trainer doors, ask the API's own question.
    /// </summary>
    /// <remarks>
    /// Three claims in one, because they are one decision. The browser cannot reference the API's
    /// assembly — it takes the generated clients and nothing else, and reaching for
    /// <c>TrainerClaims</c> would invert the dependency the boundary exists to keep — so the claim
    /// name and the policy name are written twice. That is safe exactly as long as something holds
    /// the two copies equal, and this is that something: the strings are compared to the constants
    /// the API itself registers its policy with.
    /// <para>
    /// The other two halves are structural rather than textual, so a page added tomorrow is
    /// covered without anybody remembering this rule: every routed page of the trainer's two
    /// families carries the policy, and every routed page of the administration carries the role.
    /// The defect this record answers was precisely an asymmetry between those two families —
    /// the administration guarded its pages, the trainer's space did not, and an administrator
    /// walked into a surface the API refuses them.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0078",
        "the browser asks the same question the API asks — is this caller somebody's trainer — " +
        "and the doors it offers are the doors that open")]
    public void TheBrowsersTrainerDoors_AskTheApisOwnQuestion() =>
        Doors()
            .Selected("browser door")
            .Where(door => !SourceTree.ReadText(Absolute(door.File))
                .Contains(door.Needle, StringComparison.Ordinal))
            .Select(door => door.Complaint)
            .ShouldHold();

    /// <summary>
    /// Every place the browser has to agree with the API, and what it has to say there.
    /// </summary>
    /// <remarks>
    /// Two kinds, one shape. The three vocabulary files must each carry the API's own string,
    /// because the browser writes those names again rather than referencing the assembly that
    /// declares them. The routed pages must each carry their family's guard, which is the half
    /// that keeps working when somebody adds a page next year without reading this.
    /// </remarks>
    private static IEnumerable<(string File, string Needle, string Complaint)> Doors()
    {
        yield return Vocabulary(SessionClaimsFile, TrainerClaims.TrainerId, "the claim the API mints");
        yield return Vocabulary(SessionPoliciesFile, TrainerPolicy.Name, "the policy the API registers");
        yield return Vocabulary(SessionRolesFile, IdentityRoles.Administrator, "the role the API grants");

        foreach (var page in Guarded(TrainerPages, "[Authorize(Policy = ", "the trainer's own space")
                     .Concat(Guarded(ProfilePages, "[Authorize(Policy = ", "the trainer's own space"))
                     .Concat(Guarded(AdministrationPages, "[Authorize(Roles = ", "the administration")))
        {
            yield return page;
        }
    }

    private static (string File, string Needle, string Complaint) Vocabulary(
        string file, string expected, string what) =>
        (file, $"\"{expected}\"",
            $"'{file}' does not declare '{expected}', which is {what}. The browser writes these " +
            "names again because it cannot reference the API's assembly, and a copy nothing " +
            "compares is a copy that drifts in silence (ADR 0078)");

    /// <summary>
    /// Every routed page of a family, with the guard its family carries.
    /// </summary>
    /// <remarks>
    /// Routed pages only: a dialog or a partial has no address to be refused at, and demanding an
    /// attribute of one would be asking a component to authorize a caller who never arrived.
    /// </remarks>
    private static IEnumerable<(string File, string Needle, string Complaint)> Guarded(
        string folder, string guard, string family) =>
        Directory.EnumerateFiles(Absolute(folder), "*.razor", SearchOption.AllDirectories)
            .Where(page => SourceTree.ReadText(page).Contains("@page ", StringComparison.Ordinal))
            .Select(SourceTree.Relative)
            .Select(page => (page, guard,
                $"'{page}' is a routed page of {family} and carries no '{guard}'. A bare " +
                "[Authorize] asks only whether somebody is signed in, which is how an " +
                "administrator walked into the trainer's surface and met a 403 (ADR 0078)"));

    private static string Absolute(string relative) =>
        Path.Combine(SourceTree.RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>The HTTP verbs an action answers to, its own and its route attribute's.</summary>
    private static IReadOnlySet<string> Verbs(MethodInfo action) =>
        action.GetCustomAttributes<HttpMethodAttribute>(inherit: true)
            .SelectMany(attribute => attribute.HttpMethods)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
