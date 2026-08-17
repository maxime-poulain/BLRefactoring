using System.Reflection;
using System.Text.RegularExpressions;
using FluentValidation;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Common.Results;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The shape of the CQRS stack: where a message lives, what answers it, and what it may answer with.
/// </summary>
/// <remarks>
/// Two of these rules hoist a runtime guarantee to build time. <c>ValidationPipelineBehavior</c>
/// throws when a command arrives with no validator — at dispatch, on the request that needed it —
/// and <c>AddValidatorsFromAssembly</c> scans exactly one assembly, so a validator that is correctly
/// named but placed elsewhere is never registered and never runs. Neither failure is visible in a
/// diff. Both are visible here.
/// </remarks>
public sealed partial class MessagingRules
{
    /// <summary>A rule in a validator: the property it names, and the chain that judges it.</summary>
    /// <remarks>
    /// The chain runs to the semicolon, so a rule folded over several lines — which is how the
    /// training validators write theirs — is read as the one statement it is.
    /// </remarks>
    [GeneratedRegex(@"RuleFor\(\s*\w+\s*=>\s*\w+\.(?<property>\w+)\s*\)(?<chain>[^;]*);", RegexOptions.Singleline)]
    private static partial Regex ValidationRule { get; }

    /// <summary>
    /// The verbs that judge a value's shape rather than its meaning.
    /// </summary>
    /// <remarks>
    /// Presence and size, which the contract's annotations already declare at model binding — before
    /// the pipeline runs, so a rule here is at best a second opinion and at worst a stricter one
    /// only one host holds.
    /// </remarks>
    private static readonly string[] ShapeVerbs =
        ["NotEmpty(", "NotNull(", "EmailAddress(", "Matches(", "Length(", "MaximumLength(", "MinimumLength("];
    // Spelled in two halves, like the error-vocabulary rule for the same reason: a rule that forbids
    // a token cannot write it, or it finds itself. The scan below is path-scoped and would not reach
    // this file, but the convention is cheaper to keep than to re-derive.
    private const string ExceptionConstruction = "new Validation" + "Exception(";

    private static IReadOnlyList<Type> CqrsTypes { get; } =
    [
        .. Solution.CqrsApplication.DeclaredTypes()
            .Concat(Solution.CqrsInfrastructure.DeclaredTypes())
            .Where(type => type is { IsInterface: false, IsAbstract: false })
    ];

    /// <summary>
    /// Every message, lives in the application layer.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#use-cases",
        "a command and a query are application vocabulary, and they live in the application layer")]
    public void EveryMessage_LivesInTheApplicationLayer() =>
        CqrsTypes
            .Where(type => typeof(ICommandBase).IsAssignableFrom(type) || typeof(IQuery).IsAssignableFrom(type))
            .Selected("command or query")
            .Where(type => type.Assembly != Solution.CqrsApplication)
            .Select(type => $"{type.FullName} is a message declared outside the application layer")
            .ShouldHold();

    /// <summary>
    /// Every query handler, lives in infrastructure.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#use-cases",
        "the read side is infrastructure by nature: a query handler projects straight from the database")]
    public void EveryQueryHandler_LivesInInfrastructure() =>
        CqrsTypes
            .Where(type => Implements(type, typeof(IQueryHandler<,>)))
            .Selected("query handler")
            .Where(type => type.Assembly != Solution.CqrsInfrastructure)
            .Select(type =>
                $"{type.FullName} answers a query from outside the infrastructure layer, where the " +
                "DbContext it needs does not belong")
            .ShouldHold();

    /// <summary>
    /// Every command handler, lives in the application layer.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#use-cases",
        "a command handler orchestrates the domain, so it sits with the command it answers")]
    public void EveryCommandHandler_LivesInTheApplicationLayer() =>
        CqrsTypes
            .Where(type => Implements(type, typeof(ICommandHandler<,>)))
            .Selected("command handler")
            .Where(type => type.Assembly != Solution.CqrsApplication)
            .Select(type => $"{type.FullName} answers a command from outside the application layer")
            .ShouldHold();

    /// <summary>
    /// Every command, answers with a bare result.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#use-cases",
        "a command reports whether it worked and nothing else; what changed is read back through a query")]
    public void EveryCommand_AnswersWithABareResult() =>
        CqrsTypes
            .Select(type => (type, contract: Closed(type, typeof(ICommand<>))))
            .Where(pair => pair.contract is not null)
            .Selected("command")
            .Where(pair => pair.contract!.GetGenericArguments()[0] != typeof(Result))
            .Select(pair =>
                $"{pair.type.FullName} answers with {pair.contract!.GetGenericArguments()[0].Name} " +
                "rather than a bare Result. The constraint on ICommand<T> permits Result<T>, and no " +
                "command uses it: a write that hands back what it wrote is a read in disguise")
            .ShouldHold();

    /// <summary>
    /// No query handler, answers with a domain type.
    /// </summary>
    [Fact]
    [ArchitectureRule("0001",
        "a query answers with a DTO — never with an aggregate, an entity or a value object")]
    public void NoQueryHandler_AnswersWithADomainType() =>
        CqrsTypes
            .Select(type => (type, contract: Closed(type, typeof(IQueryHandler<,>))))
            .Where(pair => pair.contract is not null)
            .Selected("query handler")
            .SelectMany(pair => Unwrap(pair.contract!.GetGenericArguments()[1])
                .Select(answered => (pair.type, answered)))
            .Where(pair => pair.answered.Assembly == Solution.Domain || pair.answered.Assembly == Solution.Kernel)
            .Select(pair =>
                $"{pair.type.FullName} answers with {pair.answered.Name}, which belongs to the domain. " +
                "A read model that is an aggregate hands callers the write side's vocabulary")
            .ShouldHold();

    /// <summary>
    /// Every query handler, answers with a dto.
    /// </summary>
    [Fact]
    [ArchitectureRule("0001",
        "a query answers with a type named for what it is: a DTO, paged or not")]
    public void EveryQueryHandler_AnswersWithADto() =>
        CqrsTypes
            .Select(type => (type, contract: Closed(type, typeof(IQueryHandler<,>))))
            .Where(pair => pair.contract is not null)
            .Selected("query handler")
            .SelectMany(pair => Unwrap(pair.contract!.GetGenericArguments()[1])
                .Select(answered => (pair.type, answered)))
            .Where(pair => !pair.answered.Name.EndsWith("Dto", StringComparison.Ordinal))
            .Select(pair => $"{pair.type.FullName} answers with {pair.answered.Name}, which is not a DTO")
            .ShouldHold();

    /// <summary>
    /// The verbs a query may begin with, written out so widening the set is a visible edit.
    /// </summary>
    /// <remarks>
    /// <c>Search</c> earns its place rather than being tolerated: seeking along an inverted index is
    /// a different act from fetching by identifier, and it is the word the Search Indexing context
    /// publishes (ADR 0059). The other four are here to be available, not because each is used.
    /// </remarks>
    private static readonly string[] RetrievalVerbs = ["Get", "Search", "Retrieve", "List", "Find"];

    /// <summary>
    /// The verbs that name their own narrowing, and so are excused from naming a criterion.
    /// </summary>
    /// <remarks>
    /// A query that fetches has a criterion; a query that searches <em>is</em> one. The catalog's
    /// search narrows by a term, a set of shelves and an order, all optional and none of them the
    /// question — <c>SearchCatalogByTermAndTopicsAndOrderQuery</c> is where naming them all leads.
    /// This is a list rather than a special case for one class so that the exemption is a property
    /// of the verb, checkable, and widening it is a visible edit.
    /// </remarks>
    private static readonly string[] VerbsThatNameTheirOwnNarrowing = ["Search"];

    /// <summary>
    /// Every query, is named for what it retrieves.
    /// </summary>
    /// <remarks>
    /// The population is taken by reflection rather than off the source tree, so a query added under
    /// a folder no glob covers is judged like any other. The third clause is the one this rule
    /// exists for: <c>GetMyTrainingsQuery</c> named its audience and left its criterion unsaid, and
    /// nothing in this suite could tell. The <c>PageRequest</c> is excluded because paging says how
    /// much of an answer is wanted rather than which answer.
    /// <para>
    /// A read <em>port</em> is out of scope on purpose — <c>ICatalogDetailQuery</c> and its family
    /// are named questions an outer layer asks an adapter (ADR 0028, ADR 0055), not messages — and
    /// the population makes that structural: a port is an interface, and interfaces are excluded
    /// from <c>CqrsTypes</c> before this rule sees them.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0081",
        "a query starts with a retrieval verb, says what it retrieves, and ends with its criterion " +
        "as ByX whenever it has one")]
    public void EveryQuery_IsNamedForWhatItRetrieves()
    {
        var queries = CqrsTypes
            .Where(type => typeof(IQuery).IsAssignableFrom(type))
            .Selected("query");

        queries
            .Where(query => !RetrievalVerbs.Any(verb => query.Name.StartsWith(verb, StringComparison.Ordinal)))
            .Select(query =>
                $"{query.Name} does not begin with a retrieval verb. The commands beside it start " +
                $"with the verb of what they do, and a query says how it reads: {string.Join(", ", RetrievalVerbs)}")
            .ShouldHold();

        queries
            .Where(query => !query.Name.EndsWith("Query", StringComparison.Ordinal))
            .Select(query =>
                $"{query.Name} implements IQuery and is not named for it. Every rule that finds a " +
                "message finds it structurally, so a query named anything at all would dispatch, " +
                "validate, page and answer with no build noticing")
            .ShouldHold();

        queries
            .Where(query => !VerbsThatNameTheirOwnNarrowing.Any(verb =>
                query.Name.StartsWith(verb, StringComparison.Ordinal)))
            .Where(query => Scoped(query) && !query.Name.Contains("By", StringComparison.Ordinal))
            .Select(query =>
                $"{query.Name} carries {string.Join(", ", Criteria(query))} and names no criterion. " +
                "A query scoped by something says so as ByX, or its name describes an audience or a " +
                "screen rather than the question it asks")
            .ShouldHold();
    }

    /// <summary>Whether a query narrows its answer by anything other than the page asked for.</summary>
    private static bool Scoped(Type query) => Criteria(query).Count > 0;

    /// <summary>What a query narrows by: everything it declares that is not its paging.</summary>
    private static IReadOnlyList<string> Criteria(Type query) =>
    [
        .. query.GetProperties()
            .Where(property => property.PropertyType != typeof(PageRequest))
            .Select(property => property.Name)
    ];

    /// <summary>
    /// Every message acting for its caller, says Current.
    /// </summary>
    /// <remarks>
    /// The population is semantic rather than a string match, and both halves of it are load-bearing.
    /// A handler taking <c>ICurrentUserService</c> says the caller matters; the message declaring no
    /// identifier says the caller is the <em>only</em> way to know whom the message acts on — no call
    /// site supplies it, so no call site can supply it wrongly, and nothing but the name can say so.
    /// One signal alone selects the wrong set: <c>SuspendTrainerCommand</c>'s handler knows the
    /// caller and acts on the trainer it carries, and <c>CreateTrainingCommand</c> assigns the
    /// caller as owner while carrying the identifier of what it creates — both are excluded by the
    /// identifier they declare, and adding Current to either would move the claim from the message
    /// to one of its call sites (ADR 0086).
    /// <para>
    /// The inverse clause keeps the word honest: a message may not borrow Current while carrying an
    /// identifier a call site fills, or while its handler never asks who is calling.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0086",
        "a message whose criterion is its caller says Current, and a message that carries an " +
        "explicit identifier never does")]
    public void EveryMessageActingForItsCaller_SaysCurrent()
    {
        var handlerByMessage = CqrsTypes
            .Select(type => (Handler: type,
                Contract: Closed(type, typeof(ICommandHandler<,>)) ?? Closed(type, typeof(IQueryHandler<,>))))
            .Where(pair => pair.Contract is not null)
            .ToDictionary(pair => pair.Contract!.GetGenericArguments()[0], pair => pair.Handler);

        var messages = CqrsTypes
            .Where(type => typeof(ICommandBase).IsAssignableFrom(type) || typeof(IQuery).IsAssignableFrom(type))
            .Selected("command or query")
            .Select(message => (
                Message: message,
                ActsForItsCaller: handlerByMessage.TryGetValue(message, out var handler)
                    && handler.GetConstructors()
                        .SelectMany(constructor => constructor.GetParameters())
                        .Any(parameter => parameter.ParameterType == typeof(ICurrentUserService))
                    && Array.TrueForAll(
                        message.GetProperties(),
                        property => property.PropertyType != typeof(Guid))))
            .ToList();

        messages
            .Where(entry => entry.ActsForItsCaller
                            && !entry.Message.Name.Contains("Current", StringComparison.Ordinal))
            .Select(entry =>
                $"{entry.Message.Name} carries no identifier and its handler resolves the caller " +
                "through ICurrentUserService: the caller is its criterion, and nothing but the name " +
                "can say so. Say Current — the way GetTrainingsByCurrentTrainerQuery does")
            .ShouldHold();

        messages
            .Where(entry => !entry.ActsForItsCaller
                            && entry.Message.Name.Contains("Current", StringComparison.Ordinal))
            .Select(entry =>
                $"{entry.Message.Name} says Current, but either it carries an identifier a call " +
                "site fills or its handler never asks who is calling. The word claims the caller " +
                "is the scope; here something else is")
            .ShouldHold();
    }

    /// <summary>
    /// Every command, has exactly one validator.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#a-write-end-to-end",
        "every command has exactly one validator, which the pipeline demands at dispatch")]
    public void EveryCommand_HasExactlyOneValidator()
    {
        var commands = CqrsTypes
            .Where(type => typeof(ICommandBase).IsAssignableFrom(type))
            .Selected("command");

        var validated = Validators
            .GroupBy(validator => validator.Validated)
            .ToDictionary(group => group.Key, group => group.Count());

        commands
            .Select(command => (command, count: validated.GetValueOrDefault(command, 0)))
            .Where(pair => pair.count != 1)
            .Select(pair => pair.count == 0
                ? $"{pair.command.FullName} has no validator. ValidationPipelineBehavior throws when " +
                  "it dispatches one — at run time, on the request that needed it"
                : $"{pair.command.FullName} has {pair.count} validators, and only one of them is the rule")
            .ShouldHold();
    }

    /// <summary>
    /// Every query taking an identifier, has a validator.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#a-write-end-to-end",
        "a query that takes an identifier validates it; one that takes a page is already bounded by the contract")]
    public void EveryQueryTakingAnIdentifier_HasAValidator()
    {
        var validated = Validators.Select(validator => validator.Validated).ToHashSet();

        CqrsTypes
            .Where(type => typeof(IQuery).IsAssignableFrom(type))
            .Selected("query")
            .Where(query => query.GetProperties().Any(property => property.PropertyType == typeof(Guid)))
            .Where(query => !validated.Contains(query))
            .Select(query =>
                $"{query.FullName} carries a Guid and has no validator. Every query that takes " +
                "one validates it; the ones that do not take one are bounded by [Range] at the " +
                "HTTP contract instead")
            .ShouldHold();
    }

    /// <summary>
    /// No command path, raises a validation failure as an exception.
    /// </summary>
    [Fact]
    [ArchitectureRule("0016",
        "a rejected command is a failed Result, so nothing on the write side raises one as an exception")]
    public void NoCommandPath_RaisesAValidationFailureAsAnException()
    {
        var writeSide = Path.Combine(SourceTree.RepositoryRoot, "src", "DDDWithCqrs", "Application");

        SourceTree.SourceFiles
            .Where(file => file.StartsWith(writeSide, StringComparison.Ordinal))
            .Selected("file on the CQRS write side")
            .Where(file => SourceTree.ReadText(file).Contains(ExceptionConstruction, StringComparison.Ordinal))
            .Select(file =>
                $"'{SourceTree.Relative(file)}' raises a validation failure as an exception. A command " +
                "reports one as a failed Result, which leaves through the single place a business " +
                "failure becomes a body — throwing it publishes a second error shape from the same " +
                "endpoint")
            .ShouldHold();
    }

    /// <summary>
    /// The validation code, is raised by the pipeline alone.
    /// </summary>
    [Fact]
    [ArchitectureRule("0016",
        "one place raises the validation code, so it keeps meaning 'rejected before the aggregate'")]
    public void TheValidationCode_IsRaisedByThePipelineAlone()
    {
        var sources = Path.Combine(SourceTree.RepositoryRoot, "src");

        SourceTree.SourceFiles
            .Where(file => file.StartsWith(sources, StringComparison.Ordinal))
            .Selected("source file")
            .Where(file => SourceTree.ReadText(file).Contains("ErrorCodes.Validation", StringComparison.Ordinal))
            .Select(SourceTree.Relative)
            .Where(file => !file.EndsWith("ErrorCodes.cs", StringComparison.Ordinal))
            .Where(file => !file.EndsWith("ValidationPipelineBehavior.cs", StringComparison.Ordinal))
            .Select(file =>
                $"'{file}' raises ErrorCodes.Validation. The code says a request was refused before " +
                "any aggregate was reached, which only the validation behavior is in a position to " +
                "say — anywhere else it is a failure with an owner, and the owner should name it")
            .ShouldHold();
    }

    /// <summary>
    /// Every validator, lives where the scan looks.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#a-write-end-to-end",
        "validators are discovered by scanning one assembly, so a validator outside it never runs")]
    public void EveryValidator_LivesWhereTheScanLooks() =>
        Validators
            .Selected("validator")
            .Where(validator => validator.Type.Assembly != Solution.CqrsApplication)
            .Select(validator =>
                $"{validator.Type.FullName} is registered by nothing: AddValidatorsFromAssembly scans " +
                "the assembly holding the commands, and this is not it. It would pass every test and " +
                "never run")
            .ShouldHold();

    /// <summary>
    /// Every message, has a handler named after it.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#use-cases",
        "every message is answered, and the handler is named after the message it answers")]
    public void EveryMessage_HasAHandlerNamedAfterIt()
    {
        var handlers = CqrsTypes
            .Where(type => Implements(type, typeof(ICommandHandler<,>)) || Implements(type, typeof(IQueryHandler<,>)))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        CqrsTypes
            .Where(type => typeof(ICommandBase).IsAssignableFrom(type) || typeof(IQuery).IsAssignableFrom(type))
            .Selected("message")
            .Where(message => !handlers.Contains($"{message.Name}Handler"))
            .Select(message =>
                $"{message.FullName} has no handler called {message.Name}Handler. A message nothing " +
                "answers fails at dispatch, and only then")
            .ShouldHold();
    }

    /// <summary>
    /// No query handler, takes a repository.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#use-cases",
        "the write side loads aggregates through repositories; the read side never does")]
    public void NoQueryHandler_TakesARepository() =>
        CqrsTypes
            .Where(type => Implements(type, typeof(IQueryHandler<,>)))
            .Selected("query handler")
            .SelectMany(handler => handler.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.ParameterType.Name.EndsWith("Repository", StringComparison.Ordinal))
                .Select(parameter => $"{handler.FullName} takes {parameter.ParameterType.Name}"))
            .ShouldHold();

    /// <summary>
    /// No command handler, takes a db context.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#use-cases",
        "the write side speaks to the domain; it never opens the DbContext itself")]
    public void NoCommandHandler_TakesADbContext() =>
        CqrsTypes
            .Where(type => Implements(type, typeof(ICommandHandler<,>)))
            .Selected("command handler")
            .SelectMany(handler => handler.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.ParameterType.Name.EndsWith("Context", StringComparison.Ordinal))
                .Select(parameter => $"{handler.FullName} takes {parameter.ParameterType.Name}"))
            .ShouldHold();

    /// <summary>
    /// Every way this codebase has of submitting a message to be handled, spelled as types.
    /// </summary>
    /// <remarks>
    /// The repository's own door and the library's: <c>IMediator</c> and <c>ISender</c> can send
    /// any command directly, which is why only the dispatch adapters hold them.
    /// <c>IQueryDispatcher</c> is deliberately absent — it cannot submit a command, and the two
    /// rules below forbid submitting, not reading.
    /// </remarks>
    private static readonly Type[] WaysToDispatch =
        [typeof(ICommandDispatcher), typeof(Mediator.IMediator), typeof(Mediator.ISender)];

    /// <summary>
    /// No command handler, takes a dispatcher.
    /// </summary>
    /// <remarks>
    /// A handler is the last link in a command's execution. A command sent from inside one
    /// re-enters the whole pipeline — validation, a second unit of work — in the middle of the
    /// first, and the workflow it starts is written nowhere a caller can see. Sending is the
    /// caller's decision: a controller's today, perhaps an integration event consumer's or a
    /// scheduler's tomorrow (ADR 0046) — never the handler's. Judged on real dependencies rather
    /// than source text, so a rename or an alias changes nothing this rule reads.
    /// </remarks>
    [Fact]
    [ArchitectureRule("README#use-cases",
        "a handler executes the command it receives and never dispatches another; sending is its " +
        "caller's decision, made above it")]
    public void NoCommandHandler_TakesADispatcher() =>
        CqrsTypes
            .Where(type => Implements(type, typeof(ICommandHandler<,>)))
            .Selected("command handler")
            .SelectMany(handler => Dependencies(handler)
                .Where(WaysToDispatch.Contains)
                .Select(dependency =>
                    $"{handler.Name} takes {dependency.Name}. A handler is the last link in a " +
                    "command's execution: it executes the command it receives, and sending " +
                    "another is its caller's decision, made above it"))
            .ShouldHold();

    /// <summary>
    /// No domain event handler, takes a dispatcher.
    /// </summary>
    /// <remarks>
    /// Stricter than the command handlers' case, because of when it runs: a domain event handler
    /// reacts inside the transaction, before the commit (ADR 0002), so a command dispatched from
    /// one re-enters the pipeline in the middle of a unit of work that has not decided yet.
    /// <para>
    /// An integration event consumer is deliberately outside this rule. It runs after the commit,
    /// and ADR 0046 already counts it among the dispatcher's possible callers — post-commit
    /// orchestration by command is a future this repository has reserved, and closing it would be
    /// a new decision rather than this one.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("README#use-cases",
        "a domain event handler reacts inside the transaction and never dispatches a command; " +
        "post-commit orchestration belongs to the callers ADR 0046 names")]
    public void NoDomainEventHandler_TakesADispatcher() =>
        Solution.Backend
            .SelectMany(assembly => assembly.DeclaredTypes())
            .Where(type => type is { IsInterface: false, IsAbstract: false })
            .Where(type => Implements(type, typeof(IDomainEventHandler<>)))
            .Selected("domain event handler")
            .SelectMany(handler => Dependencies(handler)
                .Where(WaysToDispatch.Contains)
                .Select(dependency =>
                    $"{handler.Name} takes {dependency.Name}. A domain event handler runs inside " +
                    "the transaction: a command dispatched from it re-enters the pipeline " +
                    "mid-commit, and the workflow it starts is visible to no caller"))
            .ShouldHold();

    /// <summary>
    /// What a type depends on: constructor parameters, declared fields — primary-constructor
    /// captures included — and declared properties, looking through one level of generics so a
    /// dependency wrapped in <c>Lazy&lt;&gt;</c> or <c>Func&lt;&gt;</c> is still the dependency.
    /// </summary>
    private static IEnumerable<Type> Dependencies(Type handler) =>
        handler.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .Concat(handler
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(field => field.FieldType))
            .Concat(handler
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(property => property.PropertyType))
            .SelectMany(dependency => dependency.IsGenericType
                ? dependency.GetGenericArguments().Prepend(dependency)
                : new[] { dependency })
            .Distinct();

    /// <summary>
    /// Every use case, has its own folder.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#use-cases",
        "one folder per use case, under the aggregate it belongs to")]
    public void EveryUseCase_HasItsOwnFolder()
    {
        var root = Path.Combine(SourceTree.RepositoryRoot, "src", "DDDWithCqrs", "Application");

        SourceTree.SourceFiles
            .Where(file => file.StartsWith(root, StringComparison.Ordinal))
            .Selected("file in the CQRS application layer")
            .Select(file => SourceTree.Relative(file)["src/DDDWithCqrs/Application/".Length..])
            .Where(relative => !IsAUseCase(relative))
            .Select(relative =>
                $"'{relative}' sits outside Features/<Aggregate>/<UseCase>/. The layer used to " +
                "carry one deliberate exception, Pagination/, until paging became kernel vocabulary")
            .ShouldHold();
    }

    /// <summary>A validator, and what it validates.</summary>
    /// <remarks>
    /// Matched on the shape of the base type rather than on <c>AbstractValidator&lt;&gt;</c> by name,
    /// so this suite states a rule about the codebase without taking a dependency on the validation
    /// library to do it.
    /// </remarks>
    /// <summary>
    /// No command validator, judges shape.
    /// </summary>
    /// <remarks>
    /// ADR 0016 measured this and deferred it: the duplicated rules were "already unreachable"
    /// behind the contract's own annotations, except on the registration path, where the request
    /// bounded nothing at all. ADR 0042 gave that request a contract to be bounded in, so the rules
    /// can go — and with them the one that was never a duplicate but a divergence,
    /// <c>EmailAddress()</c>, which no layered validator has and which makes the CQRS host refuse
    /// addresses the other accepts.
    /// <para>
    /// Identifiers are the exception, and they are the decision rather than a hole in it: an empty
    /// <c>Guid</c> reaching <c>EntityId.Create</c> throws, which is a 500, and neither the contract
    /// — <c>Guid.Empty</c> is a perfectly well-formed <c>Guid</c> — nor the domain can refuse it
    /// politely. That is the one thing left for this layer to guard. See ADR 0043.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0043",
        "the contract declares shape and presence, the domain judges meaning, and the validator guards what neither can")]
    public void NoCommandValidator_JudgesShape() =>
        SourceTree.SourceFiles
            .Where(file => SourceTree.Relative(file).EndsWith("CommandValidator.cs", StringComparison.Ordinal))
            .Selected("command validator")
            .SelectMany(file => ValidationRule
                .Matches(SourceTree.ReadText(file))
                .Select(rule => (
                    File: SourceTree.Relative(file),
                    Property: rule.Groups["property"].Value,
                    Chain: rule.Groups["chain"].Value)))
            .Where(rule => !rule.Property.EndsWith("Id", StringComparison.Ordinal))
            .SelectMany(rule => ShapeVerbs
                .Where(verb => rule.Chain.Contains(verb, StringComparison.Ordinal))
                .Select(verb =>
                    $"{rule.File} judges the shape of {rule.Property} with {verb.TrimEnd('(')}(). " +
                    "The contract declares shape at model binding, before this runs; a second " +
                    "opinion here is either dead or a divergence only one host has"))
            .ShouldHold();

    /// <summary>
    /// Every identifier a message carries, is refused empty by its own validator.
    /// </summary>
    /// <remarks>
    /// The HTTP contract refuses <c>Guid.Empty</c> at model binding, on both hosts (ADR 0046). That
    /// closes the only entry point there is today, and it closes exactly that one: a command reaching
    /// <c>ICommandDispatcher</c> from an integration event consumer, a background service or a
    /// scheduler never passes a controller, and would arrive unguarded.
    /// <para>
    /// So the rule is stated twice on purpose, and this is the half that does not depend on the
    /// transport. The application layer declares the preconditions of its own messages rather than
    /// assuming a layer it cannot see has already checked them — which is also why the duplication is
    /// not the kind ADR 0043 removed. That record deleted a rule that made <em>two hosts disagree
    /// about one request</em>; these two guards answer two different callers.
    /// </para>
    /// <para>
    /// Written as a rule rather than a comment for the reason this suite exists: the guard's whole
    /// value is that it survives the day somebody decides it looks redundant. It has been deleted
    /// once already.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0046",
        "the application layer refuses an empty identifier on its own messages, whatever entry point " +
        "dispatched them — it never assumes the boundary checked first")]
    public void EveryIdentifierAMessageCarries_IsRefusedEmptyByItsValidator()
    {
        var validators = Validators.ToDictionary(validator => validator.Validated, validator => validator.Type);

        CqrsTypes
            .Where(type => typeof(ICommandBase).IsAssignableFrom(type) || typeof(IQuery).IsAssignableFrom(type))
            .SelectMany(message => message
                .GetProperties()
                .Where(property => property.PropertyType == typeof(Guid))
                .Select(property => (Message: message, Property: property)))
            .Selected("identifier on a command or a query")
            .Where(entry => !IsRefusedEmpty(validators.GetValueOrDefault(entry.Message), entry.Property.Name))
            .Select(entry =>
                $"{entry.Message.Name}.{entry.Property.Name} is an identifier its validator does not " +
                "refuse empty. Over HTTP the contract stops Guid.Empty first, but a dispatcher has " +
                "other callers, and there the value would reach EntityId.Create and throw. Declare " +
                $"RuleFor(m => m.{entry.Property.Name}).NotEmpty()")
            .ShouldHold();
    }

    /// <summary>
    /// Whether a validator declares a <c>NotEmpty</c> rule for the named member.
    /// </summary>
    /// <remarks>
    /// Asked of FluentValidation's own descriptor rather than of the source text, so a rule spelled
    /// across several lines, or reached through a chain, answers the same as one written inline. The
    /// component's <c>Name</c> is the validator's public name — <c>"NotEmptyValidator"</c> — which is
    /// what the library uses to build an error code, so it is a documented value rather than a type
    /// name that could be an implementation detail.
    /// </remarks>
    private static bool IsRefusedEmpty(Type? validator, string member)
    {
        if (validator is null)
        {
            return false;
        }

        var descriptor = ((IValidator)Activator.CreateInstance(validator)!).CreateDescriptor();

        return descriptor
            .GetRulesForMember(member)
            .SelectMany(rule => rule.Components)
            .Any(component => component.Validator.Name is "NotEmptyValidator");
    }

    private static IReadOnlyList<(Type Type, Type Validated)> Validators { get; } =
    [
        .. Solution.CqrsApplication.DeclaredTypes()
            .Concat(Solution.CqrsInfrastructure.DeclaredTypes())
            .Where(type => type.BaseType is { IsGenericType: true })
            .Where(type => type.BaseType!.GetGenericTypeDefinition().Name
                .StartsWith("AbstractValidator", StringComparison.Ordinal))
            .Select(type => (type, type.BaseType!.GetGenericArguments()[0]))
    ];

    private static bool Implements(Type type, Type openInterface) => Closed(type, openInterface) is not null;

    private static Type? Closed(Type type, Type openInterface) =>
        type.GetInterfaces().FirstOrDefault(contract =>
            contract.IsGenericType && contract.GetGenericTypeDefinition() == openInterface);

    /// <summary>
    /// What a query actually answers with, looking through the paging envelope.
    /// </summary>
    /// <remarks>
    /// <c>PagedResult&lt;TrainingDto&gt;</c> is a page of DTOs, not a type of its own, so the rules
    /// about what a query may answer apply to what is inside it.
    /// </remarks>
    private static IEnumerable<Type> Unwrap(Type answered) =>
        answered.IsGenericType ? answered.GetGenericArguments() : new[] { answered };

    private static bool IsAUseCase(string relative)
    {
        var segments = relative.Split('/');

        return segments.Length == 4
               && segments[0] == "Features";
    }
}
