using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Architecture.Tests.Traceability;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The numbers the living documents state about the code, computed from the code.
/// </summary>
/// <remarks>
/// The project graph, the bounded contexts and the event-storming boards are already held to the
/// model. Between those guarded regions sits prose that counts — how many handlers the application
/// layer holds, how many endpoints the API publishes, how many records the index lists — and none
/// of it was checked. Over eleven merges that produced six wrong numbers and two documents
/// contradicting themselves in the same file, which is the exact drift this repository exists to
/// argue against. See ADR 0038.
/// <para>
/// A ledger rather than a sweep: a rule hunting every number in every document would trip over
/// "the two hosts" and over a version. Each claim declares the sentence it anchors on, and an
/// anchor that stops matching fails just as loudly as a number that stops being true — a claim
/// nobody checks any more is worse than a wrong one, because nothing will ever say so.
/// </para>
/// </remarks>
public sealed partial class DocumentationRules
{
    /// <summary>Any run of whitespace, including the line breaks the documents wrap on.</summary>
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace { get; }

    /// <summary>A code as the table publishes it, between backticks.</summary>
    [GeneratedRegex(@"`(?<code>[^`]+)`")]
    private static partial Regex BacktickedCode { get; }

    /// <summary>
    /// The counted claims the living documents make, each with the anchor that locates it and the
    /// function that computes what it should say.
    /// </summary>
    /// <remarks>
    /// Records are deliberately absent: a merged record is never rewritten, so the numbers frozen
    /// inside one are true as of its decision and are allowed to age.
    /// </remarks>
    private static readonly (string Document, string Anchor, string Subject, Func<int> Truth)[] CountedClaims =
    [
        // README — the architecture and the model
        ("README.md", @"(?<count>[\w-]+) architecture rules keep those documents",
            "rules holding the strategic design", () => RulesDeclaredBy(typeof(StrategicDesignRules))),
        ("README.md", @"(?<count>[\w-]+) projects: [\w-]+ under",
            "projects in the solution", () => Projects().Count),
        ("README.md", @"projects: (?<count>[\w-]+) under `src/`",
            "projects under src/", () => Under("src/")),
        ("README.md", @"under `src/`, (?<count>[\w-]+) under `tests/`",
            "projects under tests/", () => Under("tests/")),
        ("README.md", @"\| (?<count>[\w-]+) projects — [\w-]+ test suites",
            "projects under tests/", () => Under("tests/")),
        ("README.md", @"projects — (?<count>[\w-]+) test suites",
            "test suites", () => Projects().Count(project => project.IsTestProject)),
        ("README.md", @"the (?<count>[\w-]+) domain event handlers",
            "domain event handlers", () => DomainEventHandlers().Count),
        ("README.md", @"(?<count>[\w-]+) post-commit consumers — all shared",
            "post-commit consumers", () => IntegrationEventHandlers().Count),
        ("README.md", @"Closed set of (?<count>[\w-]+) values",
            "topics", () => ValuesOf<Topic>(typeof(Topic))),
        ("README.md", @"(?<count>[\w-]+) of the [\w-]+ handlers act inside the transaction",
            "handlers acting inside the transaction",
            () => DomainEventHandlers().Count - Publishers()),
        ("README.md", @"of the (?<count>[\w-]+) handlers act inside the transaction",
            "domain event handlers", () => DomainEventHandlers().Count),
        ("README.md", @"the (?<count>[\w-]+) that belong to nobody",
            "codes the kernel declares", () => ValuesOf<ErrorCode>(KernelCodes())),
        ("README.md", @"and (?<count>[\w-]+) architecture rules hold each of those lines",
            "rules holding the specifications", () => RulesDeclaredBy(typeof(SpecificationRules))),
        ("README.md", @"(?<count>[\w-]+) endpoints, and not one of them",
            "endpoints one host publishes", Endpoints),
        ("README.md", @"including the (?<count>[\w-]+) that translate a domain event",
            "handlers translating a domain event", Publishers),
        ("README.md", @"(?<count>[\w-]+) post-commit consumers \|",
            "post-commit consumers", () => IntegrationEventHandlers().Count),
        ("README.md", @"runs (?<count>[\w-]+) probes",
            "readiness probes", Probes),

        ("README.md", @", and (?<count>[\w\s-]+?) analyzer severities",
            "analyzer severities in .editorconfig", Severities),
        ("README.md", @"sets (?<count>[\w\s-]+?) analyzer rules on",
            "analyzer severities in .editorconfig", Severities),

        // CLAUDE.md — how the repository sizes itself for a reader
        ("CLAUDE.md", @"index of (?<count>[\w-]+) architecture decision records",
            "records", () => AdrCatalog.Records.Count),
        ("CLAUDE.md", @"the same decisions as (?<count>[\w-]+) executable rules",
            "rules in the suite", () => RuleIndex.AllTests.Count),
        ("CLAUDE.md", @", and (?<count>[\w\s-]+?) analyzer severities",
            "analyzer severities in .editorconfig", Severities),

        // The strategic design — the same discipline ADR 0023 gave the model itself
        (StrategicDesign + "README.md", @"a small domain: (?<count>[\w-]+) aggregates",
            "aggregates", Aggregates),
        (StrategicDesign + "README.md", @"aggregates, (?<count>[\w-]+) domain events",
            "domain events", DomainEvents),
        (StrategicDesign + "README.md", @"domain events, (?<count>[\w-]+) use cases",
            "use cases", UseCases),
        (StrategicDesign + "README.md", @"checks (?<count>[\w-]+) things on every build",
            "rules holding the strategic design", () => RulesDeclaredBy(typeof(StrategicDesignRules))),
        (StrategicDesign + "event-storming.md", @"a domain of (?<count>[\w-]+) aggregates",
            "aggregates", Aggregates),
        (StrategicDesign + "event-storming.md", @"aggregates and (?<count>[\w-]+) events, there is",
            "domain events", DomainEvents),
        (StrategicDesign + "event-storming.md", @"(?<count>[\w-]+) events, [\w-]+ handlers, and",
            "domain events", DomainEvents),
        (StrategicDesign + "event-storming.md", @"events, (?<count>[\w-]+) handlers, and",
            "domain event handlers", () => DomainEventHandlers().Count),

        // A policy commits exactly one fact — the publishing handlers are named after the domain
        // event they translate — so the facts feeding a port also count the policies that commit
        // them. Two sentences say it that way, and both went stale on the same merge.
        (StrategicDesign + "context-map.md", @"committed to the outbox by (?<count>[\w-]+) policies in",
            "policies feeding Notification", () => FactsFeeding(EmailPort).Count),
        (StrategicDesign + "context-map.md", @"committed to the outbox by (?<count>[\w-]+) policies \(ADR",
            "policies feeding Search Indexing", () => FactsFeeding(IndexerPort).Count),
        (StrategicDesign + "event-storming.md", @"\*\*(?<count>[\w-]+) events, [\w-]+ facts, one future consumer",
            "facts feeding Search Indexing", () => FactsFeeding(IndexerPort).Count),
        (StrategicDesign + "event-storming.md", @"events, (?<count>[\w-]+) facts, one future consumer",
            "facts feeding Search Indexing", () => FactsFeeding(IndexerPort).Count),
    ];

    /// <summary>
    /// The named lists the living documents state, each with the anchor that locates the
    /// enumeration and the function that computes what it should hold.
    /// </summary>
    /// <remarks>
    /// The anchor captures the enumeration as a <c>list</c> group, and the members are read out of
    /// it as the backticked names the prose already writes. Each row names the port in its own
    /// anchor rather than relying on the sentence alone: the two seams in <c>context-map.md</c> are
    /// worded alike, and a shared anchor would check one context's facts against the other's.
    /// </remarks>
    private static readonly (string Document, string Anchor, string Subject, Func<IReadOnlySet<string>> Truth)[] NamedLists =
    [
        (StrategicDesign + "bounded-contexts.md",
            @"Registration and address changes commit (?<list>.+?) with the change itself",
            "the facts that feed Notification", () => FactsFeeding(EmailPort)),
        (StrategicDesign + "bounded-contexts.md",
            @"a training commits (?<list>.+?) with it",
            "the facts that feed Search Indexing", () => FactsFeeding(IndexerPort)),
        (StrategicDesign + "bounded-contexts.md",
            @"the facts that will maintain its index — (?<list>.+?) — already land durably",
            "the facts that feed Search Indexing", () => FactsFeeding(IndexerPort)),
        (StrategicDesign + "context-map.md",
            @"`Shared\.Application/Notifications/IEmailSender\.cs`.+?the facts that feed it: (?<list>.+?), committed",
            "the facts that feed Notification", () => FactsFeeding(EmailPort)),
        (StrategicDesign + "context-map.md",
            @"`Shared/ITrainingSearchIndexer\.cs`.+?the facts that feed it: (?<list>.+?), committed",
            "the facts that feed Search Indexing", () => FactsFeeding(IndexerPort)),
        (StrategicDesign + "context-map.md",
            @"The facts it would subscribe to are now durable — (?<list>.+?) land in the transactional outbox",
            "the facts that feed Search Indexing", () => FactsFeeding(IndexerPort)),
    ];

    private const string StrategicDesign = "docs/strategic-design/";

    private const string IndexerPort = "ITrainingSearchIndexer";

    private const string EmailPort = "IEmailSender";

    /// <summary>
    /// Every counted claim, agrees with the code.
    /// </summary>
    /// <remarks>
    /// Two ways to fail, and the second matters more. A number that no longer matches the code is
    /// the defect everybody expects. An anchor that no longer matches anything is the defect that
    /// hides: the sentence was reworded, the rule went on passing, and the claim left the guarded
    /// set without anybody deciding it should. So rewording a guarded sentence is a build failure
    /// by design — the number travels with the words.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0038",
        "a number the documentation states about the code is derived from the code, or it is not stated")]
    public void EveryCountedClaim_AgreesWithTheCode() =>
        CountedClaims
            .Selected("counted claim")
            .SelectMany(claim =>
            {
                var matches = Regex.Matches(AsOneLine(claim.Document), claim.Anchor).ToArray();

                if (matches.Length == 0)
                {
                    return
                    [
                        $"the sentence this claim anchors on is gone from {claim.Document} " +
                        $"(/{claim.Anchor}/, about {claim.Subject}). Re-anchor the claim on the " +
                        "sentence as it now reads, or drop the row — an anchor that matches nothing " +
                        "checks nothing"
                    ];
                }

                var truth = claim.Truth();

                return matches
                    .Select(match => match.Groups["count"].Value)
                    .Where(stated => Number(stated) != truth)
                    .Select(stated =>
                        $"{claim.Document} says '{stated}' where the code has {truth} {claim.Subject}");
            })
            .ShouldHold();

    /// <summary>
    /// Every named list, agrees with the code.
    /// </summary>
    /// <remarks>
    /// The sibling of the rule above, for the claims that enumerate rather than count. A number
    /// going stale is visible to a reader who recounts; a list going stale is invisible even to one
    /// who does, because a list short by one still reads as complete — which is how the search
    /// index came to be described as fed by two facts in six sentences while three consumers
    /// depended on its port. Checked in both directions, and the second one matters: a member the
    /// prose adds is a name a reader would grep for and never find. See ADR 0041.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0041",
        "a list of code the documentation states is derived from the code, or it is not stated")]
    public void EveryNamedList_AgreesWithTheCode() =>
        NamedLists
            .Selected("named list")
            .SelectMany(claim =>
            {
                var matches = Regex.Matches(AsOneLine(claim.Document), claim.Anchor).ToArray();

                if (matches.Length == 0)
                {
                    return
                    [
                        $"the sentence this claim anchors on is gone from {claim.Document} " +
                        $"(/{claim.Anchor}/, about {claim.Subject}). Re-anchor the claim on the " +
                        "sentence as it now reads, or drop the row — an anchor that matches nothing " +
                        "checks nothing"
                    ];
                }

                var truth = claim.Truth();

                return matches.SelectMany(match =>
                {
                    var stated = BacktickedCode
                        .Matches(match.Groups["list"].Value)
                        .Select(name => name.Groups["code"].Value)
                        .ToHashSet(StringComparer.Ordinal);

                    return truth
                        .Where(member => !stated.Contains(member))
                        .Select(member =>
                            $"{claim.Document} lists {claim.Subject} without naming '{member}'. A " +
                            "list short by one reads as complete, which is why this is a rule and " +
                            "not a review")
                        .Concat(stated
                            .Where(member => !truth.Contains(member))
                            .Select(member =>
                                $"{claim.Document} names '{member}' among {claim.Subject}, and the " +
                                "code has nothing of that name feeding it"));
                });
            })
            .ShouldHold();

    /// <summary>
    /// The error-code table, lists every code.
    /// </summary>
    /// <remarks>
    /// The table is presented as the vocabulary of the API's <c>domainErrors</c> extension, which
    /// makes it contract documentation rather than illustration. Checked in both directions: a code
    /// the table omits is a hole a client cannot branch on, and a row naming no code invites a
    /// client to branch on something that will never arrive. It was missing nine of twenty-one when
    /// this rule was written — every photo code and every transfer code.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0038",
        "the error-code table is the published vocabulary, complete in both directions: every code listed, every listing a code")]
    public void TheErrorCodeTable_ListsEveryCode()
    {
        var listed = ErrorCodeTable();
        var declared = DeclaredCodes();

        declared
            .Selected("declared error code")
            .Where(code => !listed.Contains(code))
            .Select(code =>
                $"'{code}' is declared by the code and missing from the error-code table in README.md")
            .Concat(listed
                .Where(code => !declared.Contains(code))
                .Select(code =>
                    $"the error-code table in README.md lists '{code}', which no *ErrorCodes holder declares"))
            .ShouldHold();
    }

    /// <summary>
    /// Every operation named in the generator, exists.
    /// </summary>
    /// <remarks>
    /// ADR 0010 wrote this failure down in advance — "an entry that matches nothing wraps nothing,
    /// silently: the setting has no way to tell a typo from a deliberate omission" — and could not
    /// act on it, because nothing in the suite read that file. It arrived anyway:
    /// <c>TrainerClient.GetById</c> outlived the endpoint it named by several releases. The
    /// expected spelling follows from <c>OperationIdTransformer</c> and the generator's own
    /// settings — <c>{controller}Client</c> as the class template, the operation identifier split
    /// on its underscore.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0038",
        "a generator setting that names an operation names one that exists, since an entry matching nothing is silent")]
    public void EveryOperationNamedInTheGenerator_Exists()
    {
        var published = Operations();

        NamedOperations()
            .Selected("operation named in generator.nswag")
            .Where(named => !published.Contains(named.Operation))
            .Select(named =>
                $"generator.nswag names '{named.Operation}' under {named.Setting}, and no action " +
                "publishes it. NSwag matches these by name and says nothing when one matches " +
                "nothing — remove the entry, or restore the operation")
            .ShouldHold();
    }

    // ------------------------------------------------------------------ what the documents say

    /// <summary>
    /// A document, read as one line: a sentence that wraps in the source is still one sentence, and
    /// an anchor should not have to know where the paragraph was folded.
    /// </summary>
    private static string AsOneLine(string document) =>
        Whitespace.Replace(
            SourceTree.ReadText(Path.Combine(
                SourceTree.RepositoryRoot,
                document.Replace('/', Path.DirectorySeparatorChar))),
            " ");

    /// <summary>The codes the README's error-code table lists, by their published value.</summary>
    private static IReadOnlySet<string> ErrorCodeTable()
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        var inside = false;

        foreach (var line in SourceTree.ReadLines(Path.Combine(SourceTree.RepositoryRoot, "README.md")))
        {
            if (line.StartsWith("| Holder ", StringComparison.Ordinal))
            {
                inside = true;
                continue;
            }

            if (inside && !line.StartsWith('|'))
            {
                break;
            }

            var cells = inside ? line.Split('|') : [];

            // The separator row under the header is table syntax, not content.
            if (cells.Length < 3 || cells[1].Trim().All(character => character is '-' or ' '))
            {
                continue;
            }

            foreach (var code in BacktickedCode.Matches(cells[2]).Select(match => match.Groups["code"].Value))
            {
                codes.Add(code);
            }
        }

        if (codes.Count == 0)
        {
            // Vacuity: a renamed header would empty the table and make "every code is listed" true
            // by listing nothing at all.
            throw new InvalidOperationException(
                "README.md holds no '| Holder | Codes |' table. This rule reads it to compare the " +
                "published vocabulary with the declared one; with the table not found, it asserts nothing.");
        }

        return codes;
    }

    /// <summary>The operations <c>generator.nswag</c> names, with the setting that names them.</summary>
    private static IReadOnlyList<(string Setting, string Operation)> NamedOperations()
    {
        using var document = JsonDocument.Parse(
            SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, "generator.nswag")));

        var generator = document.RootElement
            .GetProperty("codeGenerators")
            .GetProperty("openApiToCSharpClient");

        // Both settings take the same "{Class}.{Method}" spelling and rot the same way; the second
        // is empty today, which is exactly when a rule is cheap to write.
        return
        [
            .. new[] { "wrapResponseMethods", "protectedMethods" }
                .Where(setting => generator.TryGetProperty(setting, out _))
                .SelectMany(setting => generator.GetProperty(setting)
                    .EnumerateArray()
                    .Select(entry => (Setting: setting, Operation: entry.GetString() ?? string.Empty)))
        ];
    }

    // ------------------------------------------------------------------ what the code is

    private static IReadOnlyList<ProjectFile> Projects() => ProjectGraph.Projects;

    private static int Under(string folder) =>
        Projects().Count(project => project.RelativePath.StartsWith(folder, StringComparison.Ordinal));

    private static IReadOnlyList<Type> DomainEventHandlers() =>
    [
        .. Solution.Application.DeclaredTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .Where(type => type.GetInterfaces().Any(contract =>
                contract.IsGenericType
                && contract.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>)))
    ];

    private static IReadOnlyList<Type> IntegrationEventHandlers() =>
    [
        .. Solution.Application.DeclaredTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .Where(type => type.GetInterfaces().Any(contract =>
                contract.IsGenericType
                && contract.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)))
    ];

    /// <summary>
    /// The integration events a context is fed, named by the port its consumers speak through.
    /// </summary>
    /// <remarks>
    /// Nothing is pinned: the answer is the consumers themselves, grouped by the port their
    /// constructor takes. A fourth consumer therefore changes the answer without anybody editing
    /// the ledger, which is the whole point — a consumer arriving is the merge that made six
    /// sentences wrong.
    /// </remarks>
    private static IReadOnlySet<string> FactsFeeding(string port)
    {
        var facts = IntegrationEventHandlers()
            .Where(consumer => consumer
                .GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType.Name == port))
            .Select(consumer => consumer
                .GetInterfaces()
                .Single(contract => contract.IsGenericType
                    && contract.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
                .GetGenericArguments()[0]
                .Name)
            .ToHashSet(StringComparer.Ordinal);

        if (facts.Count == 0)
        {
            // Vacuity, and the only way a named list could pass while checking nothing: a port
            // renamed, no consumer matched, and "the document names every fact" made true by there
            // being no fact to name.
            throw new InvalidOperationException(
                $"No integration-event consumer takes a '{port}'. The named-list ledger reads that " +
                "population to compute what the documents should enumerate; with none found, the " +
                "rows citing this port assert nothing.");
        }

        return facts;
    }

    /// <summary>The handlers whose reaction is to publish an integration event.</summary>
    private static int Publishers() =>
        DomainEventHandlers().Count(handler =>
            handler.Name.StartsWith("PublishIntegrationEventWhen", StringComparison.Ordinal));

    private static int DomainEvents() =>
        Solution.Domain.DeclaredTypes()
            .Count(type => typeof(IDomainEvent).IsAssignableFrom(type) && !type.IsAbstract);

    private static int Aggregates() =>
        Solution.Domain.DeclaredTypes()
            .Count(type => typeof(IAggregateRoot).IsAssignableFrom(type) && !type.IsAbstract);

    /// <summary>The CQRS stack's use-case folders — one per command or query, by ADR 0016.</summary>
    private static int UseCases() =>
        SourceTree.SourceFiles
            .Select(SourceTree.Relative)
            .Where(path => path.StartsWith("src/DDDWithCqrs/Application/Features/", StringComparison.Ordinal))
            .Select(path => path.Split('/'))
            .Where(segments => segments.Length > 6)
            .Select(segments => $"{segments[4]}/{segments[5]}")
            .Distinct(StringComparer.Ordinal)
            .Count();

    /// <summary>The operations one host publishes — the two hosts publish the same set (ADR 0006).</summary>
    private static int Endpoints() =>
        Solution.LayeredApi.DeclaredTypes()
            .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal) && !type.IsAbstract)
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(action => action.GetCustomAttributes(inherit: true)
                    .Any(attribute => attribute.GetType().Name.StartsWith("Http", StringComparison.Ordinal)
                        && attribute.GetType().Name.EndsWith("Attribute", StringComparison.Ordinal)))
                .Select(action => $"{controller.Name}.{action.Name}"))
            .Distinct(StringComparer.Ordinal)
            .Count();

    /// <summary>The readiness probes the shared API layer declares.</summary>
    private static int Probes() =>
        Solution.SharedApi.DeclaredTypes()
            .Count(type => !type.IsAbstract
                && type.GetInterfaces().Any(contract => contract.Name == "IHealthCheck"));

    /// <summary>The <c>{Controller}Client.{Action}</c> names the API publishes.</summary>
    private static IReadOnlySet<string> Operations() =>
        Solution.Hosts
            .SelectMany(host => host.DeclaredTypes())
            .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal) && !type.IsAbstract)
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(action => action.GetCustomAttributes(inherit: true)
                    .Any(attribute => attribute.GetType().Name.StartsWith("Http", StringComparison.Ordinal)
                        && attribute.GetType().Name.EndsWith("Attribute", StringComparison.Ordinal)))
                .Select(action => $"{Strip(controller.Name, "Controller")}Client.{Strip(action.Name, "Async")}"))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>The analyzer severities <c>.editorconfig</c> sets, which the build turns into errors.</summary>
    private static int Severities() =>
        SourceTree.ReadLines(Path.Combine(SourceTree.RepositoryRoot, ".editorconfig"))
            .Count(line => line.StartsWith("dotnet_diagnostic.", StringComparison.Ordinal)
                && line.Contains(".severity", StringComparison.Ordinal));

    private static Type KernelCodes() =>
        Solution.Kernel.DeclaredTypes().Single(type => type.Name == "ErrorCodes");

    /// <summary>The values a holder declares as public static readonly fields of one type.</summary>
    private static int ValuesOf<TValue>(Type holder) =>
        holder.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Count(field => field.FieldType == typeof(TValue));

    /// <summary>Every code the three holders publish, as the value a client branches on.</summary>
    private static IReadOnlySet<string> DeclaredCodes() =>
        Solution.All
            .SelectMany(assembly => assembly.DeclaredTypes())
            .Where(type => type.Name.EndsWith("ErrorCodes", StringComparison.Ordinal))
            .SelectMany(holder => holder
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(field => field.FieldType == typeof(ErrorCode))
                .Select(field => ((ErrorCode)field.GetValue(null)!).Value))
            .ToHashSet(StringComparer.Ordinal);

    private static int RulesDeclaredBy(Type rules) =>
        rules.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Count(method => method.GetCustomAttribute<FactAttribute>() is not null);

    private static string Strip(string name, string suffix) =>
        name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;

    // ------------------------------------------------------------------ numbers, as prose spells them

    /// <summary>
    /// The English number words the documents use, and the tens they combine with.
    /// </summary>
    /// <remarks>
    /// No helper of this kind existed: until this record no rule asserted on a number written out
    /// in words. The table stops where the documents do — a claim needing "forty-three" adds the
    /// word, which is a smaller price than a general parser nobody reads.
    /// </remarks>
    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1,
        ["two"] = 2,
        ["three"] = 3,
        ["four"] = 4,
        ["five"] = 5,
        ["six"] = 6,
        ["seven"] = 7,
        ["eight"] = 8,
        ["nine"] = 9,
        ["ten"] = 10,
        ["eleven"] = 11,
        ["twelve"] = 12,
        ["thirteen"] = 13,
        ["fourteen"] = 14,
        ["fifteen"] = 15,
        ["sixteen"] = 16,
        ["seventeen"] = 17,
        ["eighteen"] = 18,
        ["nineteen"] = 19,
        ["twenty"] = 20,
        ["thirty"] = 30,
        ["forty"] = 40,
        ["fifty"] = 50,
        ["sixty"] = 60,
        ["seventy"] = 70,
        ["eighty"] = 80,
        ["ninety"] = 90,
    };

    /// <summary>Reads a stated count, written as digits or spelled out, hyphenated tens included.</summary>
    private static int Number(string stated)
    {
        if (int.TryParse(stated, out var digits))
        {
            return digits;
        }

        // "a hundred and sixty-one" is one number written the way the documents write it: the
        // articles and the conjunction carry nothing, the hyphen joins a ten to a unit, and
        // "hundred" multiplies what came before it.
        var parts = stated
            .Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part is not ("a" or "and"))
            .ToArray();

        if (parts.Length == 0 || parts.Any(part => part != "hundred" && !NumberWords.ContainsKey(part)))
        {
            // Not a number this table knows. Returning a value no truth function can produce makes
            // the claim fail and name itself, which is the outcome either way.
            return int.MinValue;
        }

        var total = 0;

        foreach (var part in parts)
        {
            total = part == "hundred"
                ? Math.Max(total, 1) * 100
                : total + NumberWords[part];
        }

        return total;
    }
}
