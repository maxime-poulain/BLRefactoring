using System.Reflection;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>
    /// The layers a request runs in, and therefore the layers that may never read the address.
    /// </summary>
    private static readonly string[] TheRequestPath =
    [
        "src/DDD/",
        "src/DDDWithCqrs/",
        "src/TrainingHub.Shared.Api/",
        "src/Web/"
    ];

    /// <summary>
    /// The trainer's contact address, is read only where it is sent.
    /// </summary>
    /// <remarks>
    /// ADR 0070 said the catalog withholds a contact address; ADR 0082 made the platform the channel
    /// that promise assumed, and this is what keeps the two compatible. The neighboring rule asserts
    /// that no answered contract <em>carries</em> the address, which is the symptom. This asserts the
    /// cause: the port that reads it is named only by its adapter, the composition root and the one
    /// integration-event handler that sends the mail — all of which run in the outbox worker, after
    /// the commit, with no request in sight.
    /// <para>
    /// That is a stronger claim than "no response has an Email property", because it forecloses the
    /// step before: a controller, a command handler or a Blazor page cannot obtain the address at
    /// all, so no amount of mapping could leak it. A visitor who opens the contact form causes no
    /// read of it anywhere.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0082",
        "the trainer's contact address is read in one place and it is the moment of sending: no " +
        "layer that serves a request can obtain it, so no response can disclose it")]
    public void TheTrainersContactAddress_IsReadOnlyWhereItIsSent() =>
        SourceTree.SourceFiles
            .Select(SourceTree.Relative)
            .Where(file => TheRequestPath.Any(layer => file.StartsWith(layer, StringComparison.Ordinal)))
            .Selected("source file on the path a request runs")
            .Where(file => SourceTree.ReadText(Path.Combine(
                    SourceTree.RepositoryRoot, file.Replace('/', Path.DirectorySeparatorChar)))
                .Contains("ITrainerContactQuery", StringComparison.Ordinal))
            .Select(file =>
                $"'{file}' names 'ITrainerContactQuery'. The address a trainer publishes is read " +
                "once, in the consumer that sends the message, inside the outbox worker: a layer " +
                "that serves a request must not be able to obtain it at all, which is what keeps " +
                "it out of every response by construction (ADR 0070, ADR 0082)")
            .ShouldHold();

    /// <summary>
    /// The file declaring the proxy's route table, which the rule below holds shut.
    /// </summary>
    private const string TheProxysRouteTable = "src/Web/TrainingHub.Blazor/TrainingHub.Blazor/Bff/BffExtensions.cs";

    /// <summary>
    /// The proxy forwards no contact path.
    /// </summary>
    /// <remarks>
    /// ADR 0083 put a Turnstile judgment in front of the one anonymous write, and placed it in a
    /// BFF endpoint standing at the very address the proxy used to serve. The endpoint's template
    /// outranks the catalog's catch-all, so every contact message lands on the judgment — but only
    /// while the route table names no contact path of its own. A dedicated proxied route, like the
    /// one this feature shipped with before the challenge existed, would quietly outrank the
    /// endpoint back and become a door around the toll booth. That is one deleted route away from
    /// returning, so its absence is asserted: the file that builds the routes must not name the
    /// path.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0083",
        "the proxy forwards no contact path: the BFF's own endpoint is the only door, and the " +
        "visitor's proof is judged before anything is forwarded")]
    public void TheProxy_ForwardsNoContactPath() =>
        new[] { TheProxysRouteTable }
            .Selected("file declaring the proxy's route table")
            .Where(file => SourceTree.ReadText(Path.Combine(
                    SourceTree.RepositoryRoot, file.Replace('/', Path.DirectorySeparatorChar)))
                .Contains("/contact", StringComparison.OrdinalIgnoreCase))
            .Select(file =>
                $"'{file}' names a contact path. The contact message pays a toll at the BFF's own " +
                "endpoint — the Turnstile judgment — and a proxied route for the same path would " +
                "outrank it and forward the message unjudged (ADR 0083)")
            .ShouldHold();

    /// <summary>
    /// The words a public contract must not carry: each names a fact the catalog deliberately
    /// withholds — how to reach a person off the platform, and what the moderation knows.
    /// </summary>
    private static readonly string[] PrivateWords = ["Email", "Status", "Reason", "Suspension"];

    /// <summary>
    /// No catalog contract carries a private member.
    /// </summary>
    /// <remarks>
    /// ADR 0070 decided the profile's shape by what it leaves out: no contact address, because the
    /// platform is the channel; no status and no reason, because what the moderation knows is the
    /// administration's read (ADR 0055). That was prose about absent properties — the one shape of
    /// claim nothing fails when it stops being true, since adding <c>ContactEmail</c> to a
    /// response breaks no build and no test. So the absence is asserted.
    /// <para>
    /// <b>Over what the catalog answers, and derived rather than filtered by namespace</b>
    /// (ADR 0082). The population used to be every type under <c>Contracts.Catalog</c>, which read
    /// as "the catalog names no address anywhere" and was never the decision: what ADR 0070
    /// withheld is the trainer's address, a fact this API <em>discloses</em>. The moment the
    /// platform became the channel it also began accepting a visitor's own address, on a request —
    /// the opposite kind of thing, and one a name collision would have refused.
    /// </para>
    /// <para>
    /// So the set is read off the actions themselves, through their return types and their
    /// <c>[ProducesResponseType]</c>, and walked into the properties those carry. That is stricter
    /// than the namespace filter rather than looser: a withheld word on a nested type the old
    /// population never reached now fails too.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0070",
        "the public profile is a professional face, not a person's record: no contact address, " +
        "no standing, no reason ever crosses the catalog's contracts")]
    public void NoCatalogContract_CarriesAPrivateMember() =>
        Answered()
            .SelectMany(contract => contract
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => (Contract: contract, Property: property)))
            .Selected("property a catalog contract answers with")
            .SelectMany(entry => PrivateWords
                .Where(word => entry.Property.Name.Contains(word, StringComparison.Ordinal))
                .Select(word =>
                    $"{entry.Contract.Name}.{entry.Property.Name} carries '{word}'. The catalog " +
                    "publishes a professional face and withholds the person's record: reaching " +
                    "them happens on the platform, and their standing is the administration's " +
                    "read (ADR 0055, ADR 0070)"))
            .ShouldHold();

    /// <summary>
    /// Every contract a catalog action answers with, and everything reachable from one.
    /// </summary>
    /// <remarks>
    /// Read off the actions rather than off a namespace, and closed over the property graph so a
    /// withheld word cannot hide one level down. Only types this repository declares are followed:
    /// a <c>Guid</c> and a <c>string</c> have no properties worth walking, and a framework type
    /// would walk forever.
    /// </remarks>
    private static IEnumerable<Type> Answered()
    {
        var found = new HashSet<Type>();

        var roots = Solution.Hosts
            .SelectMany(host => host.DeclaredTypes())
            .Where(type => typeof(CatalogControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(controller => controller.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .SelectMany(action => action
                .GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true)
                .Select(answer => answer.Type)
                .Append(action.ReturnType));

        foreach (var root in roots)
        {
            Walk(root, found);
        }

        return found;
    }

    /// <summary>Adds the type and everything its properties reach, unwrapping the wrappers.</summary>
    private static void Walk(Type type, HashSet<Type> found)
    {
        var subject = Unwrapped(type);

        if (subject.Assembly != typeof(CatalogControllerBase).Assembly || !found.Add(subject))
        {
            return;
        }

        foreach (var property in subject.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Walk(property.PropertyType, found);
        }
    }

    /// <summary>
    /// The contract inside a task, an action result, a nullable or a collection.
    /// </summary>
    private static Type Unwrapped(Type type)
    {
        var subject = type;

        while (subject.IsGenericType)
        {
            var argument = subject.GetGenericArguments()[0];

            if (argument == subject)
            {
                break;
            }

            subject = argument;
        }

        return subject.IsArray ? subject.GetElementType() ?? subject : subject;
    }

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
