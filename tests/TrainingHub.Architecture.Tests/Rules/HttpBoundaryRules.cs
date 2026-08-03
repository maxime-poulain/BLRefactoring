using System.Reflection;
using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
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
public sealed class HttpBoundaryRules
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
    /// Every controller, derives from one of the two shared bases.
    /// </summary>
    [Fact]
    [ArchitectureRule("0011",
        "every controller inherits the same base, so what is true of one endpoint is true of all of them")]
    public void EveryController_DerivesFromOneOfTheTwoSharedBases() =>
        Controllers
            .Selected("controller")
            .Where(controller => controller.BaseType?.Name is not ("ApiControllerBase" or "AuthControllerBase"))
            .Select(controller =>
                $"{controller.FullName} derives from {controller.BaseType?.Name}, so it inherits none of " +
                "the authorization, routing or error-shape decisions the bases carry")
            .ShouldHold();

    /// <summary>
    /// Both controller bases, are abstract.
    /// </summary>
    [Fact]
    [ArchitectureRule("0011",
        "the controller bases are abstract, or MVC discovers them as controllers of their own")]
    public void BothControllerBases_AreAbstract() =>
        Solution.SharedApi.DeclaredTypes()
            .Where(type => type.Name is "ApiControllerBase" or "AuthControllerBase")
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
            .Where(type => type.Name.EndsWith("RequestHttp", StringComparison.Ordinal)
                           || type.Name.EndsWith("ResponseHttp", StringComparison.Ordinal))
            .Where(type => type.Namespace?.Contains(".Api.Contracts", StringComparison.Ordinal) != true)
            .Select(type =>
                $"{type.FullName} is named as an HTTP contract but does not live in an " +
                "*.Api.Contracts namespace, so nothing keeps it on the boundary")
            .ShouldHold();

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
    /// No http response, publishes the concurrency token.
    /// </summary>
    [Fact]
    [ArchitectureRule("0010",
        "the concurrency token is a header, never a field of the body a client reads")]
    public void NoHttpResponse_PublishesTheConcurrencyToken() =>
        Solution.Backend
            .SelectMany(assembly => assembly.DeclaredTypes())
            .Where(type => type.Name.EndsWith("ResponseHttp", StringComparison.Ordinal))
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

    private static readonly Regex RouteParameter =
        new(@"\{(?<name>\w+)(?::(?<constraint>\w+))?\}", RegexOptions.Compiled);

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

    private static bool IsHttpContract(Type type)
    {
        var candidates = type.IsGenericType
            ? new[] { type }.Concat(type.GetGenericArguments())
            : new[] { type };

        return candidates.Any(candidate =>
            candidate.Name.EndsWith("RequestHttp", StringComparison.Ordinal)
            || candidate.Name.EndsWith("ResponseHttp", StringComparison.Ordinal));
    }
}
