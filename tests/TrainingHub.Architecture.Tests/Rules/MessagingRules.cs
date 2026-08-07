using System.Text.RegularExpressions;
using FluentValidation;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.CQS;
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
    /// Every command, has exactly one validator.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#validation",
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
    [ArchitectureRule("README#validation",
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
                "any aggregate was reached, which only the validation behaviour is in a position to " +
                "say — anywhere else it is a failure with an owner, and the owner should name it")
            .ShouldHold();
    }

    /// <summary>
    /// Every validator, lives where the scan looks.
    /// </summary>
    [Fact]
    [ArchitectureRule("README#validation",
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
