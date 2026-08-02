using System.Reflection;
using System.Runtime.CompilerServices;
using BLRefactoring.Shared.Api.Controllers;
using BLRefactoring.Shared.Application.Projections;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Infrastructure;

// Aliased rather than imported: the two hosts each declare a TrainerController, and the two
// integration suites each declare an ApiFactory. Naming them through an alias keeps both in scope
// without ceremony at the use site, and states which host is meant at the one place it matters.
using AuthHelper = BLRefactoring.Api.TestKit.AuthHelper;
// One anchor per unit-test project. They are referenced for a single rule — "nobody inherits this
// class" — which cannot be answered without seeing every project that could inherit one.
using BffSuite = BLRefactoring.Blazor.Bff.Tests.RecordingHandler;
using CqrsUnitSuite = BLRefactoring.DDDWithCqrs.Tests.Pagination.PaginationTests;
using DomainSuite = BLRefactoring.Shared.Domain.Tests.Aggregates.TrainingAggregate.ValueObjects.AcquiredSkillsTests;
using InfrastructureSuite = BLRefactoring.Shared.Infrastructure.Tests.Interceptors.AuditableEntitiesInterceptorTests;
using LayeredApplicationSuite = BLRefactoring.DDD.Application.Tests.Dtos.ProjectionsTests;
using SharedApiSuite = BLRefactoring.Shared.Api.Tests.Extensions.OpenApiDocumentGenerationTests;
using CqrsApiFactory = BLRefactoring.DDDWithCqrs.Api.IntegrationTests.Fixtures.ApiFactory;
using CqrsCommandDispatcher = BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator.MediatorCommandDispatcher;
using CqrsCreateTrainerCommand = BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create.CreateTrainerCommand;
using CqrsTrainerController = BLRefactoring.DDDWithCqrs.Api.Controller.TrainerController;
using LayeredApiFactory = BLRefactoring.DDD.Api.IntegrationTests.Fixtures.ApiFactory;
using LayeredTrainerController = BLRefactoring.DDD.Api.Controller.TrainerController;
using LayeredTrainerService = BLRefactoring.DDD.Application.Services.TrainerServices.TrainerApplicationService;

namespace BLRefactoring.Architecture.Tests.Framework;

/// <summary>
/// The assemblies the rules read, each resolved through a type that exists in exactly one of them.
/// </summary>
/// <remarks>
/// Resolved by type rather than by <c>Assembly.Load("BLRefactoring.Shared.Domain")</c>, so that
/// renaming a project is a compile error here instead of a rule that quietly stops selecting
/// anything and goes on passing.
/// <para>
/// Both API hosts declare <c>public partial class Program</c> in the global namespace, and this
/// project references both, so the bare name <c>Program</c> is ambiguous here and using it is
/// CS0433. Nothing in this suite needs it. The alternative, <c>extern alias</c>, is a
/// whole-assembly instrument for a one-identifier problem: it would take every other type of that
/// host out of the global scope too.
/// </para>
/// <para>
/// The three routing projects have no entry. They hold no source file, so there is no type to name
/// them by — their edges are checked from the csproj graph, which needs no assembly at all.
/// </para>
/// </remarks>
internal static class Solution
{
    public static readonly Assembly Kernel = typeof(Result).Assembly;

    public static readonly Assembly Domain = typeof(Trainer).Assembly;

    public static readonly Assembly Application = typeof(TrainerProjections).Assembly;

    public static readonly Assembly Infrastructure = typeof(UnitOfWork).Assembly;

    public static readonly Assembly SharedApi = typeof(ApiControllerBase).Assembly;

    public static readonly Assembly LayeredApplication = typeof(LayeredTrainerService).Assembly;

    public static readonly Assembly LayeredApi = typeof(LayeredTrainerController).Assembly;

    public static readonly Assembly CqrsApplication = typeof(CqrsCreateTrainerCommand).Assembly;

    public static readonly Assembly CqrsInfrastructure = typeof(CqrsCommandDispatcher).Assembly;

    public static readonly Assembly CqrsApi = typeof(CqrsTrainerController).Assembly;

    public static readonly Assembly TestKit = typeof(AuthHelper).Assembly;

    public static readonly Assembly LayeredSuite = typeof(LayeredApiFactory).Assembly;

    public static readonly Assembly CqrsSuite = typeof(CqrsApiFactory).Assembly;

    /// <summary>The two API hosts, for every rule asserting they publish the same thing.</summary>
    public static readonly IReadOnlyList<Assembly> Hosts = [LayeredApi, CqrsApi];

    /// <summary>The two integration suites, for the rule that the shared kit is run by both.</summary>
    public static readonly IReadOnlyList<Assembly> Suites = [LayeredSuite, CqrsSuite];

    /// <summary>Every backend assembly under inspection.</summary>
    public static readonly IReadOnlyList<Assembly> Backend =
    [
        Kernel, Domain, Application, Infrastructure, SharedApi,
        LayeredApplication, LayeredApi,
        CqrsApplication, CqrsInfrastructure, CqrsApi
    ];

    /// <summary>
    /// Everything: the product, every test project, and this suite itself.
    /// </summary>
    /// <remarks>
    /// For the rules whose statement is about all of it at once. "Nobody inherits this class" cannot
    /// be said from a partial view — a production class whose only subclass is a test double is
    /// inherited, and sealing it would not compile — so the rule that says it has to see every test
    /// project too. That is what the six unit-test references in this csproj are for; nothing else
    /// uses them.
    /// </remarks>
    public static readonly IReadOnlyList<Assembly> All =
    [
        .. Backend,
        TestKit, LayeredSuite, CqrsSuite,
        typeof(DomainSuite).Assembly,
        typeof(LayeredApplicationSuite).Assembly,
        typeof(CqrsUnitSuite).Assembly,
        typeof(SharedApiSuite).Assembly,
        typeof(InfrastructureSuite).Assembly,
        typeof(BffSuite).Assembly,
        typeof(Solution).Assembly
    ];

    /// <summary>
    /// The types a rule should look at: everything a human wrote.
    /// </summary>
    /// <remarks>
    /// Compiler-generated types are skipped — closures, iterator state machines, record
    /// scaffolding, and whatever a source generator left behind. None of them is anybody's
    /// decision, and a rule about sealedness or constructors would fail on them for a reason no
    /// reader could act on.
    /// <para>
    /// So is the code the build writes, and it needs saying separately.
    /// <see cref="SourceTree.IsGenerated"/> already excludes it from every rule that reads files,
    /// deriving that from the <c>&lt;auto-generated&gt;</c> header or the <c>.editorconfig</c>
    /// section that declares it generated. Reflection has no such header to read: a scaffolded class
    /// looks exactly like a hand-written one, and the sealing rule found two and was right to.
    /// </para>
    /// </remarks>
    public static IEnumerable<Type> DeclaredTypes(this Assembly assembly) =>
        assembly.GetTypes().Where(type =>
            !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
            && !type.Name.Contains('<', StringComparison.Ordinal)
            && !IsToolWritten(type));

    /// <summary>Whether a tool wrote this type, rather than a person.</summary>
    /// <remarks>
    /// EF marks its own output, and the marking is matched by the attribute's full name rather than
    /// by the type, so that stating this does not oblige the architecture project to reference EF
    /// Core for the sake of two strings. A migration and a model snapshot both carry
    /// <c>[DbContext]</c>; a migration carries <c>[Migration]</c> as well.
    /// <para>
    /// The test SDK's entry point is matched by name, because that is what it offers. Every test
    /// project gets an <c>AutoGeneratedProgram</c> in the global namespace, written into <c>obj/</c>
    /// by <c>Microsoft.NET.Test.Sdk</c> and compiled in — a class no repository convention can reach,
    /// since there is no file in the tree to change.
    /// </para>
    /// </remarks>
    private static bool IsToolWritten(Type type) =>
        type.FullName is "AutoGeneratedProgram"
        || type.GetCustomAttributesData().Any(attribute =>
            attribute.AttributeType.FullName is
                "Microsoft.EntityFrameworkCore.Infrastructure.DbContextAttribute"
                or "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute");
}
