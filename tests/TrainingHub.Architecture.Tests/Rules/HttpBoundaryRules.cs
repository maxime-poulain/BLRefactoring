using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using TrainingHub.Shared.Api.Contracts.Mappings;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NetArchTest.Rules;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// Where HTTP stops and the application begins, and what the two hosts owe each other.
/// </summary>
/// <remarks>
/// This repository publishes one API from two implementations, and claims a client generated from
/// either fits both. That claim is worth exactly as much as the mechanism that keeps it true. Until
/// now the mechanism was a hand-written list of seven operations out of thirteen, in a test that
/// explained it could do no better because it only ever sees the host it runs against. A project
/// that references both hosts is not under that constraint.
/// </remarks>
public sealed partial class HttpBoundaryRules
{
    private static IReadOnlyList<Type> Controllers { get; } =
    [
        .. Solution.Hosts
            .SelectMany(host => host.DeclaredTypes())
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
    ];

    private static IEnumerable<(Type Controller, MethodInfo Action)> Actions =>
        Controllers.SelectMany(controller => controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(action => action.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .Select(action => (controller, action)));

    /// <summary>
    /// Every controller, derives from one of the four shared bases.
    /// </summary>
    /// <remarks>
    /// Two until the administration got endpoints. The third exists because the second stopped being
    /// neutral: <c>ApiControllerBase</c> carries <c>TrainerPolicy</c> since ADR 0051, and a policy on
    /// an action is combined with its controller's rather than replacing it, so an administrative
    /// action could not have been hosted there.
    /// <para>
    /// The fourth arrived with the public catalogue (ADR 0059), and for the same mechanical reason
    /// read the other way round: an anonymous action hosted on either of those two would inherit a
    /// policy it cannot satisfy. It is the one base that declares no 401 and no 403, because it can
    /// answer neither.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0011",
        "every controller inherits the same base, so what is true of one endpoint is true of all of them")]
    public void EveryController_DerivesFromOneOfTheFourSharedBases() =>
        Controllers
            .Selected("controller")
            .Where(controller => controller.BaseType?.Name is not
                ("ApiControllerBase" or "AuthControllerBase" or "AdministrationControllerBase"
                 or "CatalogueControllerBase"))
            .Select(controller =>
                $"{controller.FullName} derives from {controller.BaseType?.Name}, so it inherits none of " +
                "the authorization, routing or error-shape decisions the bases carry")
            .ShouldHold();

    /// <summary>
    /// Every controller base, is abstract.
    /// </summary>
    [Fact]
    [ArchitectureRule("0011",
        "the controller bases are abstract, or MVC discovers them as controllers of their own")]
    public void EveryControllerBase_IsAbstract() =>
        Solution.SharedApi.DeclaredTypes()
            .Where(type => type.Name.EndsWith("ControllerBase", StringComparison.Ordinal))
            .Selected("shared controller base")
            .Where(type => !type.IsAbstract)
            .Select(type =>
                $"{type.FullName} is not abstract. [ApiController] derives from ControllerAttribute, " +
                "so a concrete base is discovered as a controller and publishes its own routes")
            .ShouldHold();

    /// <summary>
    /// No controller, takes a repository or a db context.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#http-is-a-boundary",
        "a controller talks to the application layer; it never opens the database itself")]
    public void NoController_TakesARepositoryOrADbContext() =>
        Controllers
            .SelectMany(controller => controller
                .GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => (controller, parameter)))
            .Selected("controller constructor parameter")
            .Where(pair => pair.parameter.ParameterType.Name.EndsWith("Repository", StringComparison.Ordinal)
                           || pair.parameter.ParameterType.Name.EndsWith("DbContext", StringComparison.Ordinal))
            .Select(pair =>
                $"{pair.controller.FullName} takes {pair.parameter.ParameterType.Name}, which puts a " +
                "persistence concern one method call from an HTTP request")
            .ShouldHold();

    /// <summary>
    /// No controller, depends on infrastructure.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#http-is-a-boundary",
        "the hosts reach infrastructure from their composition root, and from nowhere else")]
    public void NoController_DependsOnInfrastructure()
    {
        foreach (var host in Solution.Hosts)
        {
            Types.InAssembly(host)
                .That()
                .HaveNameEndingWith("Controller")
                .Should()
                .NotHaveDependencyOnAny(
                    "TrainingHub.Shared.Infrastructure",
                    "TrainingHub.DDDWithCqrs.Infrastructure",
                    "Microsoft.EntityFrameworkCore")
                .GetResult()
                .ShouldHold();
        }
    }

    /// <summary>
    /// The shared api layer, names no domain type.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#the-dependency-rule",
        "the API layer speaks to the application layer; the domain is two layers further in")]
    public void TheSharedApiLayer_NamesNoDomainType() =>
        // True since the authorization policy and the token service stopped asking a repository for
        // an aggregate to read one field out of it. Before that, this rule had two exceptions and
        // was therefore not a rule.
        Types.InAssembly(Solution.SharedApi)
            .Should()
            .NotHaveDependencyOnAny("TrainingHub.Shared.Domain")
            .GetResult()
            .ShouldHold();

    /// <summary>
    /// Every http contract, lives in a contracts namespace.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#http-is-a-boundary",
        "the HTTP contracts are the boundary's own vocabulary, and they stay on it")]
    public void EveryHttpContract_LivesInAContractsNamespace() =>
        Solution.Backend
            .SelectMany(assembly => assembly.DeclaredTypes())
            .Selected("type in the backend")
            .Where(type => IsNamedForTheBoundary(type.Name))
            .Where(type => type.Namespace?.Contains(".Api.Contracts", StringComparison.Ordinal) != true)
            .Select(type =>
                $"{type.FullName} is named as an HTTP contract but does not live in an " +
                "*.Api.Contracts namespace, so nothing keeps it on the boundary")
            .ShouldHold();

    /// <summary>
    /// Every type on the boundary, is a contract.
    /// </summary>
    /// <remarks>
    /// The converse of the rule above, and the one that can actually be violated. That one's
    /// population is <em>types already named as contracts</em>, so a type that is a contract and is
    /// not named like one is not in it — the convention was checked in the direction where breaking
    /// it is impossible. Three types had been sitting in the gap since before this suite existed:
    /// the auth request and response bodies, declared at the bottom of <c>AuthControllerBase.cs</c>
    /// as <c>RegisterRequest</c>, <c>LoginRequest</c> and <c>LoginResponse</c> — in the generated
    /// client, consumed by the Blazor front, and named by nothing that would notice.
    /// <para>
    /// The population here comes from the actions: what they bind and what they answer, unwrapped
    /// through <c>Task</c>, <c>ActionResult</c> and the generic contracts, plus the types the
    /// <c>ProducesResponseType</c> attributes declare — an action that answers a bare
    /// <c>ActionResult</c> names its body there and nowhere else. See ADR 0042.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0042",
        "every type an action binds or answers is a contract, declared under Contracts/: what the " +
        "boundary publishes comes from a closed vocabulary")]
    [ArchitectureRule("0048",
        "a published contract is named *HttpRequest or *HttpResponse, with the qualifier in front")]
    public void EveryTypeOnTheBoundary_IsAContract() =>
        Actions
            .Selected("controller action")
            .SelectMany(entry => TypesOnTheBoundary(entry.Action)
                .Select(type => (entry.Controller, entry.Action, Type: type)))
            .Where(entry => Solution.Backend.Contains(entry.Type.Assembly))
            .Where(entry => !IsAContract(entry.Type))
            .Select(entry => entry.Type)
            .Distinct()
            .Select(type =>
                $"{type.FullName} is bound or answered by an action, and is not a contract: a " +
                "published type is named *HttpRequest or *HttpResponse and declared under " +
                "Contracts/. ADR 0042 closed the boundary's vocabulary and ADR 0048 put the " +
                "qualifier in front — move it, or stop publishing it")
            .ShouldHold();

    /// <summary>Whether a type is a contract by both halves of the convention.</summary>
    private static bool IsAContract(Type type) =>
        IsNamedForTheBoundary(Bare(type))
        && type.Namespace?.Contains(".Api.Contracts", StringComparison.Ordinal) == true;

    /// <summary>
    /// Whether a name says, in front, that the type belongs to the HTTP boundary.
    /// </summary>
    /// <remarks>
    /// The one place the convention is spelled, so the rules below and the one above cannot drift
    /// apart. In front rather than at the end since ADR 0048, which is also why no rule may ask
    /// "does this end in Request" to place a type: <c>CreateTrainerHttpRequest</c> and the
    /// application layer's <c>TrainerEditionRequest</c> both do.
    /// </remarks>
    private static bool IsNamedForTheBoundary(string name) =>
        name.EndsWith("HttpRequest", StringComparison.Ordinal)
        || name.EndsWith("HttpResponse", StringComparison.Ordinal);

    /// <summary>A type's name without the arity a generic carries — <c>PagedHttpResponse`1</c>.</summary>
    private static string Bare(Type type) =>
        type.Name.Split('`', 2)[0];

    /// <summary>
    /// Every type an action puts on the wire: what it binds, what it answers, and what its
    /// <c>ProducesResponseType</c> attributes declare.
    /// </summary>
    private static IEnumerable<Type> TypesOnTheBoundary(MethodInfo action) =>
        action.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Where(type => type != typeof(CancellationToken))
            .Concat([action.ReturnType])
            .Concat(action.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true)
                .Select(attribute => attribute.Type))
            .SelectMany(Unwrap)
            .Distinct();

    /// <summary>
    /// The published types inside a declared one: <c>Task</c> and <c>ActionResult</c> are plumbing,
    /// a generic contract publishes itself and what it carries.
    /// </summary>
    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();

            if (definition == typeof(Task<>) || definition == typeof(ActionResult<>))
            {
                return type.GetGenericArguments().SelectMany(Unwrap);
            }

            return type.GetGenericArguments().SelectMany(Unwrap).Concat([type]);
        }

        return [type];
    }

    /// <summary>
    /// No inner layer, names an http contract.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#http-is-a-boundary",
        "commands, queries and application DTOs stop at the boundary: the layers below never see a contract")]
    public void NoInnerLayer_NamesAnHttpContract()
    {
        var inner = new[]
        {
            Solution.Kernel, Solution.Domain, Solution.Application,
            Solution.LayeredApplication, Solution.CqrsApplication,
            Solution.Infrastructure, Solution.CqrsInfrastructure
        };

        inner
            .SelectMany(assembly => assembly.DeclaredTypes())
            .Selected("type below the boundary")
            .SelectMany(type => type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(method => new[] { method.ReturnType }
                    .Concat(method.GetParameters().Select(parameter => parameter.ParameterType))
                    .Select(named => (type, member: method.Name, named))))
            .Where(usage => IsHttpContract(usage.named))
            .Select(usage =>
                $"{usage.type.FullName}.{usage.member} names {usage.named.Name}, an HTTP contract, " +
                "below the layer that is supposed to own it")
            .ShouldHold();
    }

    /// <summary>
    /// No inner layer, declares a type named for the transport.
    /// </summary>
    /// <remarks>
    /// The neighbour above catches an inner layer that <em>uses</em> a contract — one appearing in a
    /// public signature below the boundary. This one catches an inner layer that <em>declares</em> a
    /// type wearing the boundary's name, which no signature need ever expose, and which the neighbour
    /// would therefore miss until somebody passed it around.
    /// <para>
    /// It is the half of ADR 0048 the rename could not carry on its own. While the boundary's types ended
    /// in <c>RequestHttp</c>, the two vocabularies were separable by suffix and nothing had to say
    /// so: an application input ended in <c>Request</c>, a contract did not. Now that a contract ends
    /// in <c>HttpRequest</c>, both end in <c>Request</c>, and the only thing left that tells them
    /// apart is which assembly declares them.
    /// </para>
    /// <para>
    /// So the rule is per assembly rather than per string, and it guards the direction that matters:
    /// a type named for the transport, declared inside. The kernel, the domain, both application
    /// layers and infrastructure answer to callers that never crossed HTTP — a name from the boundary
    /// there is either a contract that leaked inwards or an inner type pretending to be one, and both
    /// are the confusion ADR 0042 gave the boundary a vocabulary to prevent.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0048",
        "the HTTP qualifier belongs to the API assemblies: an inner layer names no *HttpRequest and no " +
        "*HttpResponse, whatever its own suffixes look like")]
    public void NoInnerLayer_DeclaresATypeNamedForTheTransport() =>
        new[]
        {
            (Layer: "the kernel", Assembly: Solution.Kernel),
            (Layer: "the domain", Assembly: Solution.Domain),
            (Layer: "the shared application layer", Assembly: Solution.Application),
            (Layer: "the layered application", Assembly: Solution.LayeredApplication),
            (Layer: "the CQRS application layer", Assembly: Solution.CqrsApplication),
            (Layer: "infrastructure", Assembly: Solution.Infrastructure),
            (Layer: "the CQRS infrastructure layer", Assembly: Solution.CqrsInfrastructure)
        }
            .SelectMany(layer => layer.Assembly.DeclaredTypes().Select(type => (layer.Layer, Type: type)))
            .Selected("type declared inside the boundary")
            .Where(entry => IsNamedForTheBoundary(Bare(entry.Type)))
            .Select(entry =>
                $"{entry.Type.FullName} is named for the HTTP boundary and is declared in " +
                $"{entry.Layer}. A contract belongs under *.Api.Contracts; what this layer takes is a " +
                "*Request and what it answers is a *Dto, and neither wears the transport's name")
            .ShouldHold();

    /// <summary>
    /// No http response, publishes the concurrency token.
    /// </summary>
    [Fact]
    [ArchitectureRule("0010",
        "the concurrency token is a header, never a field of the body a client reads")]
    public void NoHttpResponse_PublishesTheConcurrencyToken() =>
        Solution.Backend
            .SelectMany(assembly => assembly.DeclaredTypes())
            .Where(type => type.Name.EndsWith("HttpResponse", StringComparison.Ordinal))
            .Selected("HTTP response contract")
            .SelectMany(type => type.GetProperties().Select(property => (type, property)))
            .Where(pair => pair.property.Name.Equals("RowVersion", StringComparison.Ordinal))
            .Select(pair =>
                $"{pair.type.FullName} publishes RowVersion in its body. The version travels as an " +
                "ETag; a second copy in the payload is a second thing to keep in step")
            .ShouldHold();

    /// <summary>
    /// Both hosts, publish the same operations.
    /// </summary>
    [Fact]
    [ArchitectureRule("0008",
        "a client generated from either host fits both, which is only true if both publish the same operations")]
    public void BothHosts_PublishTheSameOperations()
    {
        var layered = Published(Solution.LayeredApi).Selected("operation published by the layered host");
        var cqrs = Published(Solution.CqrsApi).Selected("operation published by the CQRS host");

        layered.Except(cqrs, StringComparer.Ordinal)
            .Select(operation => $"'{operation}' is published by the layered host and not by the CQRS one")
            .Concat(cqrs.Except(layered, StringComparer.Ordinal)
                .Select(operation => $"'{operation}' is published by the CQRS host and not by the layered one"))
            .ShouldHold();
    }

    /// <summary>
    /// Both hosts, answer each operation with the same shape.
    /// </summary>
    /// <remarks>
    /// The rule above proves the two hosts publish the same operation names; this one proves the
    /// names mean the same thing. The distinction is not hypothetical: for as long as only the
    /// CQRS host paged, both hosts published <c>Training_GetMine</c> while one answered a bare
    /// array and the other a page envelope — and the client generated from the layered document
    /// could not deserialise the CQRS host's answer. Same names over different bodies is a parity
    /// no client can use.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0029",
        "both hosts answer the same list the same way — one operation, one shape, whichever host serves it")]
    public void BothHosts_AnswerEachOperationWithTheSameShape()
    {
        var layered = Answers(Solution.LayeredApi);
        var cqrs = Answers(Solution.CqrsApi);

        layered.Keys.Intersect(cqrs.Keys, StringComparer.Ordinal)
            .Selected("operation published by both hosts")
            .Where(operation => !layered[operation].SequenceEqual(cqrs[operation], StringComparer.Ordinal))
            .Select(operation =>
                $"'{operation}' answers [{string.Join(", ", layered[operation])}] on the layered host " +
                $"and [{string.Join(", ", cqrs[operation])}] on the CQRS one — one generated client " +
                "cannot serve both")
            .ShouldHold();
    }

    /// <summary>
    /// No action, answers a bare collection.
    /// </summary>
    [Fact]
    [ArchitectureRule("0029",
        "a collection leaves as one page of a total order, on either host — a bare array is an unbounded read")]
    public void NoAction_AnswersABareCollection() =>
        Actions
            .Selected("action")
            .SelectMany(pair => pair.Action
                .GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true)
                .Where(produces => produces.StatusCode is >= 200 and < 300 && IsBareCollection(produces.Type))
                .Select(produces => (pair.Controller, pair.Action, produces)))
            .Select(hit =>
                $"{hit.Controller.Assembly.GetName().Name}: {hit.Controller.Name}.{hit.Action.Name} " +
                $"declares {hit.produces.Type.Name} for its {hit.produces.StatusCode} — a collection " +
                "leaves as a page envelope, or the read is unbounded and grows with the data")
            .ShouldHold();

    /// <summary>
    /// Every action, declares what it can answer.
    /// </summary>
    [Fact]
    [ArchitectureRule("0004",
        "every action states the statuses it can answer, so the document describes the API and not a subset")]
    public void EveryAction_DeclaresWhatItCanAnswer() =>
        Actions
            .Selected("action")
            .Where(pair => !pair.Action.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true).Any())
            .Select(pair =>
                $"{pair.Controller.Name}.{pair.Action.Name} declares no [ProducesResponseType], so the " +
                "generated client has no idea what it answers")
            .ShouldHold();

    /// <summary>
    /// Every identifier in a route, is constrained.
    /// </summary>
    [Fact]
    [ArchitectureRule("0011",
        "an identifier in a route is a Guid, and the route says so rather than finding out in the action")]
    public void EveryIdentifierInARoute_IsConstrained() =>
        Actions
            .SelectMany(pair => pair.Action
                .GetCustomAttributes<HttpMethodAttribute>(inherit: true)
                .Select(verb => verb.Template)
                .Where(template => template is not null)
                .SelectMany(template => RouteParameter.Matches(template!))
                .Select(match => (pair.Controller, pair.Action, match)))
            .Selected("route parameter")
            .Where(entry => entry.match.Groups["name"].Value.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                            && !entry.match.Groups["constraint"].Success)
            .Select(entry =>
                $"{entry.Controller.Name}.{entry.Action.Name} binds " +
                $"'{{{entry.match.Groups["name"].Value}}}' without a :guid constraint, so a caller's " +
                "typo reaches the action rather than the router")
            .ShouldHold();

    [GeneratedRegex(@"\{(?<name>\w+)(?::(?<constraint>\w+))?\}")]
    private static partial Regex RouteParameter { get; }

    /// <summary>
    /// What a host declares each of its operations answers: every
    /// <c>[ProducesResponseType]</c>, as "status → type", sorted so two hosts can be compared.
    /// </summary>
    /// <remarks>
    /// Read from the attributes rather than from the emitted documents, for the same reason
    /// <see cref="Published"/> is: this project references both hosts, so the comparison is a
    /// dictionary lookup instead of two host processes. The attributes are what the document
    /// generator reads, and <c>EveryAction_DeclaresWhatItCanAnswer</c> guarantees no action
    /// answers something they do not say.
    /// </remarks>
    private static Dictionary<string, string[]> Answers(Assembly host) =>
        Controllers
            .Where(controller => controller.Assembly == host)
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(action => action.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .Select(action => (Operation: OperationId(controller, action), Action: action)))
            .ToDictionary(
                pair => pair.Operation,
                pair => pair.Action
                    .GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true)
                    .Select(produces => $"{produces.StatusCode} → {produces.Type}")
                    .OrderBy(answer => answer, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

    /// <summary>
    /// Whether a declared answer is a collection with no envelope around it.
    /// </summary>
    /// <remarks>
    /// <c>byte[]</c> is exempt: an array of bytes is one resource's body — the photo — not a list
    /// of resources that grows with the data, and no page envelope could serve it.
    /// </remarks>
    private static bool IsBareCollection(Type type) =>
        (type.IsArray && type != typeof(byte[]))
        || (type.IsGenericType && type.GetGenericTypeDefinition() is var definition
            && (definition == typeof(List<>)
                || definition == typeof(IEnumerable<>)
                || definition == typeof(ICollection<>)
                || definition == typeof(IReadOnlyCollection<>)
                || definition == typeof(IReadOnlyList<>)));

    /// <summary>The operation identifiers one host publishes.</summary>
    private static IEnumerable<string> Published(Assembly host) =>
        Controllers
            .Where(controller => controller.Assembly == host)
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(action => action.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .Select(action => OperationId(controller, action)))
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// The operation identifier the document will carry, derived the way the transformer derives it.
    /// </summary>
    /// <remarks>
    /// <c>OperationIdTransformer</c> writes <c>{ControllerName}_{ActionName}</c> from MVC's own
    /// names, and MVC strips the <c>Async</c> suffix before it gets there — which is why the
    /// document says <c>Trainer_GetCurrent</c> for a method called <c>GetCurrentAsync</c>.
    /// </remarks>
    private static string OperationId(Type controller, MethodInfo action)
    {
        var controllerName = controller.Name.EndsWith("Controller", StringComparison.Ordinal)
            ? controller.Name[..^"Controller".Length]
            : controller.Name;

        var actionName = action.Name.EndsWith("Async", StringComparison.Ordinal)
            ? action.Name[..^"Async".Length]
            : action.Name;

        return $"{controllerName}_{actionName}";
    }

    /// <summary>
    /// Every identifier an action takes, is refused empty.
    /// </summary>
    /// <remarks>
    /// Unannotated, the value travels to <c>EntityId.Create</c>, which refuses the empty one by
    /// throwing — a 500 on the layered host, where no validation pipeline stands in the way, against
    /// the 400 the CQRS host answered for the same request. <c>[NotEmptyIdentifier]</c> is answered
    /// at model binding by <c>[ApiController]</c>, with the <c>ValidationProblemDetails</c> this API
    /// already answers everywhere else, and it is answered identically on both hosts because both
    /// carry the annotation (ADR 0046).
    /// <para>
    /// The annotation sits on the parameter rather than on a route contract of its own, and that was
    /// measured rather than assumed: a complex <c>[FromRoute]</c> model emits its property's
    /// description onto the operation's <em>request body</em> whenever the action also carries one,
    /// which put "The training being addressed" on the body of <c>PUT /Training/{trainingId}</c> in
    /// the published document. Reordering the parameters did not move it. A contract that lies about
    /// the payload is worse than a parameter that carries its own guard.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0046",
        "an identifier a caller supplies carries the annotation that refuses the empty one, where the client " +
        "reads it and the boundary answers it")]
    public void EveryIdentifierAnActionTakes_IsRefusedEmpty() =>
        Actions
            .SelectMany(entry => entry.Action.GetParameters()
                .Select(parameter => (entry.Controller, entry.Action, Parameter: parameter)))
            .Selected("action parameter")
            .Where(entry => entry.Parameter.ParameterType == typeof(Guid)
                && !entry.Parameter.IsDefined(typeof(NotEmptyIdentifierAttribute), inherit: true))
            .Select(entry =>
                $"{entry.Controller.Name}.{entry.Action.Name} takes '{entry.Parameter.Name}' as an " +
                "unguarded Guid. Guid.Empty then reaches EntityId.Create, which throws — a 500 where " +
                "the caller deserves a 400. Mark it [NotEmptyIdentifier]")
            .ShouldHold();

    /// <summary>
    /// No contract, marks an identifier required.
    /// </summary>
    /// <remarks>
    /// <see cref="System.ComponentModel.DataAnnotations.RequiredAttribute"/> on a non-nullable
    /// <see cref="Guid"/> is satisfied by <see cref="Guid.Empty"/> — the value is always present, so
    /// the annotation refuses nothing while reading exactly like a guard. This is not hypothetical:
    /// <c>TransferTrainingHttpRequest.RecipientTrainerId</c> carried it, and the contract's own
    /// remark claimed it "only refuses a message with no recipient at all" (ADR 0046).
    /// </remarks>
    [Fact]
    [ArchitectureRule("0046",
        "a non-nullable Guid never carries [Required] in a contract: there the annotation refuses nothing " +
        "and reads as a guard")]
    public void NoContract_MarksAnIdentifierRequired() =>
        Solution.SharedApi.DeclaredTypes()
            .Where(IsHttpContract)
            .SelectMany(contract => contract
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => (Contract: contract, Property: property)))
            .Selected("contract property")
            .Where(entry => entry.Property.PropertyType == typeof(Guid)
                && entry.Property.IsDefined(typeof(RequiredAttribute), inherit: true))
            .Select(entry =>
                $"{entry.Contract.Name}.{entry.Property.Name} is a non-nullable Guid marked [Required], " +
                "which Guid.Empty satisfies. Use [NotEmptyIdentifier], which refuses what this one only " +
                "appears to")
            .ShouldHold();

    /// <summary>
    /// Every state a published response can report, carries its reason.
    /// </summary>
    /// <remarks>
    /// The boundary half of <c>EveryReasonedState_IsWrittenWithItsReason</c>. That rule holds the
    /// pairing where the state is written, on the aggregate; this one holds it where the state is
    /// read, on the contract the decision's subject actually opens. A sanction the domain records
    /// with its reason and the API publishes without one is a mute sanction all the same — the
    /// person it lands on cannot tell the difference (ADR 0057).
    /// <para>
    /// Both directions, because each fails differently. A state without a reason is a decision
    /// nobody can account for; a reason without a state is a field whose meaning depends on knowing,
    /// from somewhere else, when it is filled in.
    /// </para>
    /// <para>
    /// Paired by name rather than by count, because the name says which state a reason explains —
    /// which is why the withheld and the suspended vocabularies never merged into one
    /// <c>Reason</c> (ADR 0015).
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0057",
        "a trainer's own surface reports every state it can be in, with the reason for it")]
    public void EveryStateAResponseReports_CarriesItsReason() =>
        Solution.SharedApi
            .DeclaredTypes()
            .Where(type => type.Name.EndsWith("HttpResponse", StringComparison.Ordinal))
            .Selected("published response")
            .SelectMany(ReasonedStateViolations)
            .ShouldHold();

    /// <summary>The two ways the pairing breaks, stated apart because they read differently.</summary>
    private static IEnumerable<string> ReasonedStateViolations(Type response)
    {
        var members = response.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var hasStatus = members.Any(member => member.Name == "Status");

        var reasons = members
            .Where(member => member.Name.EndsWith("Reason", StringComparison.Ordinal))
            .Select(member => member.Name)
            .ToList();

        if (hasStatus && reasons.Count == 0)
        {
            yield return $"{response.Name} publishes a Status and no reason for it. A caller reading " +
                "a state they did not choose has no account of it, which is the mute sanction " +
                "ADR 0057 closes";
        }

        if (!hasStatus && reasons.Count > 0)
        {
            yield return $"{response.Name} publishes {string.Join(" and ", reasons)} and no Status. " +
                "A reason belongs beside the state that gives it meaning (ADR 0052), on the boundary " +
                "as much as on the aggregate";
        }
    }

    /// <summary>
    /// Every mapping to a published contract, assigns every member of it.
    /// </summary>
    /// <remarks>
    /// The defect this exists for happened twice in eight pull requests, and silently both times: a
    /// member was added to a read model and to the contract beside it, and the translation between
    /// them compiled without copying it. **A member that is not <c>required</c> is not missed by an
    /// object initialiser** — so the compiler is content, the tests pass, and the API serves a
    /// <see langword="null"/> on the column the change existed to add.
    /// <para>
    /// Stated by running the mapping rather than by reading it. The suite carries no Roslyn, and a
    /// regex over an initialiser would be a claim about how the code is punctuated rather than about
    /// what it does: this fills the source with values that are nothing like a default, invokes the
    /// translation, and asks whether anything came out still at one. A forgotten member is exactly a
    /// member left at its default, whichever way the mapping is written.
    /// </para>
    /// <para>
    /// The population takes itself from the signatures: a public static method of the mapping class
    /// taking one read model and answering one published contract. A translation added tomorrow
    /// joins it by existing, which is the direction that fails safely.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0058",
        "a translation to a published contract is total: a member the mapping forgets is a null the " +
        "API serves and nobody asked for")]
    public void EveryMappingToAPublishedContract_AssignsEveryMember() =>
        typeof(ApplicationToHttpMappings)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(IsATranslationToAContract)
            .Selected("translation to a published contract")
            .SelectMany(UnassignedMembers)
            .ShouldHold();

    /// <summary>One read model in, one published contract out.</summary>
    private static bool IsATranslationToAContract(MethodInfo method)
    {
        var parameters = method.GetParameters();

        return parameters.Length == 1
            && parameters[0].ParameterType.Name.EndsWith("Dto", StringComparison.Ordinal)
            && method.ReturnType.Name.EndsWith("HttpResponse", StringComparison.Ordinal);
    }

    private static IEnumerable<string> UnassignedMembers(MethodInfo translation)
    {
        var source = Filled(translation.GetParameters()[0].ParameterType);
        var published = translation.Invoke(obj: null, [source])!;

        return published.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(member => LooksUntouched(member.GetValue(published)))
            .Select(member =>
                $"{translation.DeclaringType!.Name}.{translation.Name}" +
                $"({translation.GetParameters()[0].ParameterType.Name}) leaves " +
                $"{published.GetType().Name}.{member.Name} at its default, from a source where " +
                "nothing was. A member the translation forgets is a null the API serves and nobody " +
                "asked for (ADR 0058)");
    }

    /// <summary>
    /// An instance whose every member is something no mapping could have produced by accident.
    /// </summary>
    /// <remarks>
    /// Uninitialised rather than constructed, because a read model's members are <c>required</c> and
    /// <c>init</c>: the first refuses an object initialiser this cannot write, and the second is
    /// settable by reflection all the same.
    /// </remarks>
    private static object Filled(Type readModel)
    {
        var instance = RuntimeHelpers.GetUninitializedObject(readModel);

        foreach (var member in readModel.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(member => member.CanWrite))
        {
            member.SetValue(instance, SomethingUnlike(member.PropertyType));
        }

        return instance;
    }

    /// <summary>The generic shapes a published list may be declared as.</summary>
    private static readonly Type[] ListLike =
    [
        typeof(List<>),
        typeof(IReadOnlyList<>),
        typeof(IReadOnlyCollection<>),
        typeof(ICollection<>),
        typeof(IEnumerable<>)
    ];

    private static object SomethingUnlike(Type type)
    {
        var bare = Nullable.GetUnderlyingType(type) ?? type;

        if (bare == typeof(string))
        {
            return "a value no default would produce";
        }

        if (bare == typeof(Guid))
        {
            return Guid.Parse("11111111-1111-1111-1111-111111111111");
        }

        if (bare == typeof(byte[]))
        {
            return new byte[] { 1 };
        }

        // A date, because default(DateTime) is what an unassigned one looks like and the arithmetic
        // conversion below cannot produce one. The first contract to carry a date is ADR 0061's,
        // and the rule could not fill it — it threw rather than reporting, which is a rule that
        // stops checking the moment it meets a member type nobody had used yet.
        if (bare == typeof(DateTime))
        {
            return new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        }

        if (bare == typeof(DateTimeOffset))
        {
            return new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.Zero);
        }

        // A list, however it is declared. A contract may publish one as List<T> or behind one of the
        // read-only interfaces, and both look identical to LooksUntouched — so both have to be
        // fillable, or a member declared the second way is silently exempt from the rule.
        if (bare.IsGenericType && ListLike.Contains(bare.GetGenericTypeDefinition()))
        {
            var list = (System.Collections.IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(bare.GetGenericArguments()[0]))!;
            list.Add(SomethingUnlike(bare.GetGenericArguments()[0]));

            return list;
        }

        return bare == typeof(bool) ? true : Convert.ChangeType(1, bare, CultureInfo.InvariantCulture);
    }

    /// <summary>Whether a member of the published contract came out looking like nothing happened.</summary>
    private static bool LooksUntouched(object? value) => value switch
    {
        null => true,
        string text => text.Length == 0,
        Guid identifier => identifier == Guid.Empty,
        System.Collections.ICollection collection => collection.Count == 0,
        _ => value.Equals(Activator.CreateInstance(value.GetType()))
    };

    private static bool IsHttpContract(Type type)
    {
        var candidates = type.IsGenericType
            ? new[] { type }.Concat(type.GetGenericArguments())
            : new[] { type };

        return candidates.Any(candidate => IsNamedForTheBoundary(candidate.Name));
    }
}
