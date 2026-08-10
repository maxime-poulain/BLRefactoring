using System.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Blazor.Bff;
using TrainingHub.Shared.Api.Controllers;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The one family of paths a caller reaches without a token, and the two places that must agree
/// on it (ADR 0062).
/// </summary>
/// <remarks>
/// The proxy opened a hole in a wall that had none, and a hole is only as safe as the thing behind
/// it. What makes this one defensible is not the path — it is that the controller serving it
/// declares no 401 and no 403, reads a table composed of what a visitor may be shown, and has an
/// architecture rule keeping it that way. Widen either half alone and the argument stops holding:
/// a proxy forwarding <c>/api/**</c> without a policy turns every authenticated endpoint into one
/// answered by whoever asks, and a controller base adopted by a second controller quietly extends
/// what the open path reaches.
/// <para>
/// So the two are compared rather than each checked against a list. A list is a third place to
/// forget.
/// </para>
/// </remarks>
public sealed class AnonymousAccessRules
{
    /// <summary>
    /// The proxy's anonymous paths, are exactly the API's anonymous controllers.
    /// </summary>
    /// <remarks>
    /// Read off the configuration the host builds rather than off the text of the file that builds
    /// it: what a reverse proxy forwards is decided by <see cref="RouteConfig.AuthorizationPolicy"/>
    /// and <see cref="RouteMatch.Path"/> together, and a rule matching on source text would be
    /// satisfied by a path that never became a route.
    /// <para>
    /// The route builder is private, which is correct — nothing outside that file composes routes —
    /// so it is reached by reflection, and the reflection is the whole point of the arrangement:
    /// this rule reads what the host will actually serve. It fails loudly if the method is renamed,
    /// rather than selecting nothing and going on passing.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0062",
        "the proxy forwards one family of paths without a token, and it is exactly the family the " +
        "API serves from the base that declares no 401 and no 403")]
    public void TheProxysAnonymousPaths_AreExactlyTheApisAnonymousControllers()
    {
        var forwardedWithoutAToken = ForwardedWithoutAToken();
        var servedWithoutAToken = ServedWithoutAToken();

        forwardedWithoutAToken
            .Except(servedWithoutAToken, StringComparer.OrdinalIgnoreCase)
            .Select(prefix =>
                $"the proxy forwards '/api/{prefix}' without an authorization policy, and no " +
                "controller deriving from CatalogControllerBase serves it. An open path in front " +
                "of a guarded endpoint answers 401 at best and publishes it at worst (ADR 0062)")
            .Concat(servedWithoutAToken
                .Except(forwardedWithoutAToken, StringComparer.OrdinalIgnoreCase)
                .Select(prefix =>
                    $"'{prefix}' is served from CatalogControllerBase, which declares no 401 and " +
                    "no 403, and the proxy forwards it only with a session. The endpoint is open " +
                    "and unreachable, which is the shape ADR 0059 left behind (ADR 0062)"))
            .ShouldHold();
    }

    /// <summary>
    /// The path prefixes the reverse proxy forwards with no authorization policy of its own.
    /// </summary>
    private static IReadOnlyList<string> ForwardedWithoutAToken() =>
        Routes()
            .Selected("route the proxy serves")
            .Where(route => !string.Equals(
                route.AuthorizationPolicy, "default", StringComparison.OrdinalIgnoreCase))
            .Select(route => Prefix(route.Match.Path
                ?? throw new InvalidOperationException(
                    $"Route '{route.RouteId}' matches on no path at all, which this rule cannot " +
                    "compare against a controller.")))
            .ToList();

    /// <summary>
    /// The route prefixes both hosts serve from the base that declares no refusal.
    /// </summary>
    /// <remarks>
    /// Distinct, because every operation is published twice and the two hosts name their catalog
    /// controllers identically on purpose (<c>BothHosts_PublishTheSameOperations</c>).
    /// </remarks>
    private static IReadOnlyList<string> ServedWithoutAToken() =>
        Solution.Hosts
            .SelectMany(host => host.DeclaredTypes())
            .Where(type => typeof(CatalogControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .Selected("controller serving anonymous callers")
            .Select(controller => controller.Name.Replace(
                "Controller", string.Empty, StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>The segment a proxy path opens, without the API prefix or the catch-all.</summary>
    private static string Prefix(string path) =>
        path[BffExtensions.ApiPrefix.Length..].Trim('/').Split('/')[0];

    /// <summary>The routes the host composes, reached where it composes them.</summary>
    private static IReadOnlyList<RouteConfig> Routes() =>
        (IReadOnlyList<RouteConfig>)(typeof(BffExtensions)
            .GetMethod("BuildRoutes", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "BffExtensions no longer declares BuildRoutes. This rule compares what the proxy " +
                "forwards with what the API opens, and it cannot do that from a method that is " +
                "not there (ADR 0062)."))
        .Invoke(obj: null, parameters: null)!;

    /// <summary>
    /// The write model's state vocabulary, in the forms a reader would reach for.
    /// </summary>
    /// <remarks>
    /// Both enumerations, and the two properties that hold them. A predicate written here would be
    /// written with one of these four, because there is nothing else to write it with.
    /// </remarks>
    private static readonly string[] TheWriteModelsStates =
    [
        "TrainingStatus",
        "TrainerStatus",
        "IsPublished()",
        ".Status"
    ];

    /// <summary>The adapter that answers the catalog's read by identifier.</summary>
    private const string TheDetailAdapter =
        "src/TrainingHub.Shared.Infrastructure/Search/CatalogDetailQuery.cs";

    /// <summary>The index entry, which is where "on offer" is composed.</summary>
    private const string TheIndexEntry = "TrainingSearchEntry";

    /// <summary>
    /// The catalog detail, takes its visibility from the index.
    /// </summary>
    /// <remarks>
    /// The one adapter here that opens the index and the write model in the same method, which
    /// makes it the one place where a second definition of "on offer" can appear by accident. It
    /// reads the trainings table already — for the description, the topics, the trainer's name —
    /// so adding <c>&amp;&amp; candidate.Status == TrainingStatus.Published</c> to that
    /// <c>Where</c> would look like tightening a query rather than like duplicating a rule the
    /// nine consumers of ADR 0056 spend their existence composing.
    /// <para>
    /// It would also be wrong in a way no test would catch: the two definitions agree today, and
    /// they would diverge the day a tenth reason to hide a training is added to the index and not
    /// to this predicate — leaving one endpoint showing what the rest of the system has withdrawn.
    /// </para>
    /// <para>
    /// Stated as both halves at once, because either alone is satisfiable by a file that does
    /// nothing: naming the entry proves the index is asked, and naming no state proves nothing else
    /// is.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0062",
        "visibility comes from the index and content comes from the write model, and the adapter " +
        "that joins them never writes a visibility predicate of its own")]
    public void TheCatalogDetail_TakesItsVisibilityFromTheIndex()
    {
        var source = SourceTree.ReadText(Path.Combine(
            SourceTree.RepositoryRoot,
            TheDetailAdapter.Replace('/', Path.DirectorySeparatorChar)));

        var asksTheIndex = source.Contains(TheIndexEntry, StringComparison.Ordinal)
            ? Enumerable.Empty<string>()
            : [
                $"'{TheDetailAdapter}' never names '{TheIndexEntry}'. Whether a training is on " +
                "offer is what an entry in that table means, and an adapter that does not ask it " +
                "is answering the question some other way (ADR 0056, ADR 0062)"
            ];

        asksTheIndex
            .Concat(TheWriteModelsStates
                .Where(state => source.Contains(state, StringComparison.Ordinal))
                .Select(state =>
                    $"'{TheDetailAdapter}' names '{state}'. The write model says what a training " +
                    "is, and the index says whether a visitor may see it: a state read here is a " +
                    "second definition of \"on offer\", and two of those disagree eventually " +
                    "(ADR 0062)"))
            .ShouldHold();
    }

    /// <summary>
    /// The catalog names a person only by identifier, and never lists people.
    /// </summary>
    /// <remarks>
    /// ADR 0070 opened a person's public page, and ADR 0055 had withdrawn the read that lists
    /// trainers to anybody — two decisions one route apart. What separates them is the route's own
    /// shape: a profile is asked for with an identifier the visitor already holds, where a
    /// directory hands identifiers out. So the rule reads the shape: every anonymous route that
    /// says <c>trainers</c> says which trainer in the same breath, and a pageable
    /// <c>GET /Catalog/trainers</c> cannot reappear as a widening of the profile.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0070",
        "the catalog publishes a person by identifier and never a list of people: every catalog " +
        "route naming trainers names one, constrained in the route itself")]
    public void TheCatalog_NamesAPersonOnlyByIdentifier() =>
        Solution.Hosts
            .SelectMany(host => host.DeclaredTypes())
            .Where(type => typeof(CatalogControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(action => action
                    .GetCustomAttributes<HttpMethodAttribute>(inherit: true)
                    .Where(verb => verb.Template is not null)
                    .Select(verb => (Controller: controller, Action: action, Template: verb.Template!))))
            .Selected("catalog route")
            .Where(entry => Segments(entry.Template)
                .Contains("trainers", StringComparer.OrdinalIgnoreCase))
            .Where(entry => !SaysWhichTrainer(entry.Template))
            .Select(entry =>
                $"{entry.Controller.Name}.{entry.Action.Name} serves '{entry.Template}', which " +
                "names trainers without naming one. The catalog publishes a person by identifier; " +
                "the read that lists people to anybody is the one ADR 0055 withdrew (ADR 0070)")
            .ShouldHold();

    /// <summary>Whether the segment after <c>trainers</c> is one constrained identifier.</summary>
    private static bool SaysWhichTrainer(string template)
    {
        var segments = Segments(template);
        var index = Array.FindIndex(
            segments, segment => string.Equals(segment, "trainers", StringComparison.OrdinalIgnoreCase));

        return index >= 0
            && index + 1 < segments.Length
            && string.Equals(segments[index + 1], "{trainerId:guid}", StringComparison.Ordinal);
    }

    /// <summary>The template's segments, as the router reads them.</summary>
    private static string[] Segments(string template) => template.Trim('/').Split('/');
}
