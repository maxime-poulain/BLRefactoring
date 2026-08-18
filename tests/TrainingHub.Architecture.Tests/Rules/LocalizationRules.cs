using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NetArchTest.Rules;
using TrainingHub.Architecture.Tests.Framework;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The words and the language: who may know the translations, and who resolves the culture.
/// </summary>
/// <remarks>
/// ADR 0088 draws one line and wires one pipeline. The line: the domain answers stable codes and
/// the boundary answers sentences, so the resource assembly is reachable from every surface and
/// referenced by no inner layer — and references nothing itself, so it can never become a corridor
/// between two layers the graph keeps apart. The pipeline: every host resolves the request's
/// culture the same way, and every culture file carries exactly the neutral file's keys, because
/// with English as the fallback a missing translation shows nothing at all — which is why the
/// drift has to be a red build rather than a bug report from a French visitor.
/// </remarks>
public sealed partial class LocalizationRules
{
    /// <summary>
    /// The suffix an inner-circle project's name carries, per stack and shared alike.
    /// </summary>
    /// <remarks>
    /// <c>.Infrastructure</c> left the list in ADR 0090: the infrastructure projects are the
    /// interface-adapters ring, not an inner circle, and the shared one now holds the presenter
    /// that composes the verification email — fenced to its <c>Email</c> namespace by the rule
    /// below.
    /// </remarks>
    private static readonly string[] InnerCircleSuffixes = [".Domain", ".Application"];

    /// <summary>Where the resource families live, as the repository writes the path.</summary>
    private const string TranslationsRoot = "src/TrainingHub.Translations/";

    /// <summary>Where the consumers that send an email live, as the repository writes the path.</summary>
    private const string ConsumersRoot = "src/TrainingHub.Shared.Application/IntegrationEventHandlers/";

    /// <summary>Where the browser's half of this product lives, as the repository writes the path.</summary>
    private const string WebRoot = "src/Web/";

    /// <summary>The product's name, which is a name in every language and therefore not a word.</summary>
    private const string Brand = "TrainingHub";

    /// <summary>The composition roots the culture decision is about, by host.</summary>
    private static readonly string[] ApiHostPrograms =
    [
        Path.Combine("src", "DDD", "Api", "Program.cs"),
        Path.Combine("src", "DDDWithCqrs", "Api", "Program.cs"),
    ];

    /// <summary>
    /// No inner circle, references the translations.
    /// </summary>
    /// <remarks>
    /// The domain's half of this line is already held by <c>TheDomain_ReferencesTheKernelAnd
    /// NothingElse</c>; this rule is what keeps the application projects honest, because nothing
    /// else bounds what they may reference. Read from the project graph rather than compiled
    /// metadata, like every layering rule: the compiler prunes a reference no type uses yet, and
    /// the csproj is where the coupling decision is written. The infrastructure projects left the
    /// census in ADR 0090: an adapter ring is where presenters live, and the shared one now
    /// composes the verification email there — the reference it gained is fenced to its Email
    /// namespace by <see cref="OnlyTheEmailCorner_OfTheInfrastructureReadsTheTranslations"/>,
    /// which is the companion this narrowing leans on.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0088",
        "translations are presentation: the domain answers codes, the edge answers sentences, " +
        "and no use-case or entity circle references the resource assembly")]
    public void NoInnerCircle_ReferencesTheTranslations() =>
        ProjectGraph.Projects
            .Where(project => project.Name == "TrainingHub.Shared"
                              || InnerCircleSuffixes.Any(suffix =>
                                  project.Name.EndsWith(suffix, StringComparison.Ordinal)))
            .Selected("inner-circle project")
            .Where(project => project.ProjectReferences.Contains("TrainingHub.Translations"))
            .Select(project =>
                $"{project.RelativePath} references TrainingHub.Translations. A sentence's language " +
                "belongs to the caller and is known only at the boundary: an inner circle that could " +
                "read a resource would start answering in one, and the same decision would come out " +
                "worded by whoever happened to ask (ADR 0088)")
            .ShouldHold();

    /// <summary>
    /// Only the email corner, of the infrastructure reads the translations.
    /// </summary>
    /// <remarks>
    /// The fence around the exception the rule above records: the shared infrastructure may
    /// reference the resource assembly, and only its <c>Email</c> namespace may use it, because
    /// that corner is where the messages a human reads are composed and delivered (ADR 0090).
    /// <c>OnlyTheCompositionRoot_RegistersServices</c>'s shape, for its reason — a whole ring is
    /// admitted to a capability, and a namespace names the one place inside it allowed to
    /// exercise it. A repository, an interceptor or the outbox plumbing reaching for a localizer
    /// goes red here: a persisted fact never varies with a language (ADR 0026, ADR 0088).
    /// </remarks>
    [Fact]
    [ArchitectureRule("0090",
        "the words of an email are presentation: inside the shared infrastructure, only the Email " +
        "namespace — the ring's presenter and its gateway — may read the resource assembly")]
    public void OnlyTheEmailCorner_OfTheInfrastructureReadsTheTranslations() =>
        Types.InAssembly(Solution.Infrastructure)
            .That()
            .DoNotResideInNamespaceEndingWith(".Email")
            .Should()
            .NotHaveDependencyOnAny("TrainingHub.Translations")
            .GetResult()
            .ShouldHold();

    /// <summary>
    /// The translations, depend on nothing.
    /// </summary>
    /// <remarks>
    /// The generated client's rule, for the generated client's reason: an assembly that every
    /// surface loads — the WebAssembly client included — carries words and nothing else, because
    /// anything it referenced would ride into all of them, and a reference from it to any layer
    /// would open a corridor between projects the graph keeps apart.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0088",
        "the translations depend on nothing: one assembly every surface may load, carrying words alone")]
    public void TheTranslations_DependOnNothing()
    {
        var translations = ProjectGraph.Project("TrainingHub.Translations");

        translations.ProjectReferences
            .Select(reference => $"the translations reference the project '{reference}'")
            .Concat(translations.PackageReferences
                .Select(package => $"the translations reference the package '{package.Name}'"))
            .ShouldHold();
    }

    /// <summary>
    /// Every culture resource, carries exactly the defaults keys.
    /// </summary>
    /// <remarks>
    /// Exact equality rather than a subset in either direction, decided in ADR 0088: the
    /// framework's fallback means a missing key never shows raw on a screen, which is precisely
    /// why it would hide forever — an intentionally untranslated key is indistinguishable from a
    /// forgotten one, so in a showcase the silent drift is the defect. The set of languages a
    /// family must speak is read from <c>SupportedLanguages</c> itself, which also closes the
    /// other direction: a culture file for a language the list does not offer is dead weight
    /// nobody can select.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0088",
        "every culture file carries exactly the neutral file's keys, for every supported language: " +
        "fallback hides a missing translation, so the drift must be a red build instead")]
    public void EveryCultureResource_CarriesExactlyTheDefaultsKeys()
    {
        var families = SourceTree.AllFiles
            .Where(path => path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
            .Select(path => (Absolute: path, Relative: SourceTree.Relative(path)))
            .Where(file => file.Relative.StartsWith(TranslationsRoot, StringComparison.Ordinal))
            .GroupBy(file => FamilyOf(file.Relative))
            .Selected("resource family");

        var translated = DeclaredLanguages()
            .Where(language => !string.Equals(language, DeclaredDefault(), StringComparison.OrdinalIgnoreCase))
            .Selected("supported language besides the default");

        var violations = new List<string>();

        foreach (var family in families.OrderBy(family => family.Key, StringComparer.Ordinal))
        {
            var byCulture = family.ToDictionary(
                file => CultureOf(file.Relative),
                file => file,
                StringComparer.OrdinalIgnoreCase);

            if (!byCulture.TryGetValue(string.Empty, out var neutral))
            {
                violations.Add(
                    $"the family '{family.Key}' has no neutral file, so there is nothing to fall back to");
                continue;
            }

            var expected = Keys(neutral.Absolute);

            foreach (var language in translated)
            {
                if (!byCulture.TryGetValue(language, out var translation))
                {
                    violations.Add(
                        $"'{family.Key}' has no {language} file: a supported language with no " +
                        "translation would answer English out of a page in another language");
                    continue;
                }

                var keys = Keys(translation.Absolute);

                violations.AddRange(expected.Except(keys, StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .Select(key => $"{translation.Relative} misses the key '{key}'"));
                violations.AddRange(keys.Except(expected, StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .Select(key =>
                        $"{translation.Relative} carries the key '{key}', which the neutral file " +
                        "does not: a translation of nothing is a leftover"));
            }

            violations.AddRange(byCulture.Keys
                .Where(culture => culture.Length > 0
                                  && !translated.Contains(culture, StringComparer.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(culture =>
                    $"'{family.Key}' carries a {culture} file, but SupportedLanguages does not " +
                    $"offer {culture}: a translation nobody can select is dead weight, and one " +
                    "that should be selectable belongs in the list first"));
        }

        violations.ShouldHold();
    }

    /// <summary>
    /// Every domain error key, is a code the domain raises.
    /// </summary>
    /// <remarks>
    /// The funnel looks a refusal's sentence up by its code, so the catalog's keys are claims
    /// about the domain's vocabulary — and a key nothing raises is a translation of nothing,
    /// invisible until somebody wonders why a renamed code stopped answering French. The declared
    /// set is read the way the documentation rules read it: off the <c>*ErrorCodes</c> holders
    /// themselves, so a code that moves takes its key along or goes red here.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0089",
        "the error catalog is keyed by the codes the holders declare: a key nothing raises is a " +
        "translation of nothing, kept green only by never being read")]
    public void EveryDomainErrorKey_IsACodeTheDomainRaises()
    {
        var declared = DeclaredCodes();

        Keys(Path.Combine(
                SourceTree.RepositoryRoot, "src", "TrainingHub.Translations", "DomainErrorResources.resx"))
            .Selected("domain error key")
            .Where(key => !declared.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key =>
                $"DomainErrorResources.resx carries '{key}', which no *ErrorCodes holder declares. " +
                "The catalog translates the domain's vocabulary, not a guess at it (ADR 0089)")
            .ShouldHold();
    }

    /// <summary>Every code value the solution's holders declare, read off the holders themselves.</summary>
    private static IReadOnlySet<string> DeclaredCodes() =>
        Solution.All
            .SelectMany(assembly => assembly.DeclaredTypes())
            .Where(type => type.Name.EndsWith("ErrorCodes", StringComparison.Ordinal))
            .SelectMany(holder => holder
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(field => field.FieldType == typeof(ErrorCode))
                .Select(field => ((ErrorCode)field.GetValue(null)!).Value))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Both api hosts, resolve the same culture.
    /// </summary>
    /// <remarks>
    /// The logging rule's shape, for the logging rule's reason: ADR 0088 wires the culture
    /// resolution once in <c>TrainingHub.Shared.Api</c> precisely so neither host can quietly
    /// answer a language the other refuses, and that is a promise about two <c>Program.cs</c>
    /// files reflection cannot see. Comment lines are excluded before searching, so a call
    /// commented out stops counting as a call.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0088",
        "the two hosts resolve the same culture: each Program.cs calls AddApiLocalization and UseApiLocalization")]
    public void BothApiHosts_ResolveTheSameCulture() =>
        ApiHostPrograms
            .Selected("API host Program.cs")
            .SelectMany(program =>
            {
                var code = SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, program))
                    .Split('\n')
                    .Select(line => line.TrimStart())
                    .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
                    .ToArray();

                return new[] { "AddApiLocalization", "UseApiLocalization" }
                    .Where(call => !Array.Exists(code, line => line.Contains(call + "(", StringComparison.Ordinal)))
                    .Select(call =>
                        $"{program} never calls {call}. ADR 0088 resolves the culture once in " +
                        "Shared.Api precisely so neither host can answer a language the other " +
                        "refuses — add the call, or record the new decision");
            })
            .ShouldHold();

    /// <summary>
    /// Both API hosts, localize their annotations.
    /// </summary>
    /// <remarks>
    /// The presence half of the annotations bridge, in the mold of the culture rule above: the
    /// wiring lives once in <c>Shared.Api</c>, and this holds each host to calling it. Which
    /// family it wires to — and that a template's English is the key itself — is behavior, held
    /// by <c>LocalizationExtensionsTests</c> where behavior is held.
    /// </remarks>
    [Fact]
    [ArchitectureRule(
        "0089",
        "the contract's own refusals answer in the resolved culture: every template a house " +
        "attribute declares is a key in the validation family, and both hosts wire the provider")]
    public void BothApiHosts_LocalizeTheirAnnotations() =>
        ApiHostPrograms
            .Selected("API host Program.cs")
            .Where(program => !SourceTree.ReadText(Path.Combine(SourceTree.RepositoryRoot, program))
                .Split('\n')
                .Select(line => line.TrimStart())
                .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
                .Any(line => line.Contains("AddApiValidationLocalization(", StringComparison.Ordinal)))
            .Select(program =>
                $"{program} never calls AddApiValidationLocalization. ADR 0089 localizes the " +
                "data annotation templates once in Shared.Api so both hosts refuse a field in " +
                "the caller's language — add the call to the AddControllers chain, or record " +
                "the new decision")
            .ShouldHold();

    /// <summary>
    /// Every key a screen asks, exists in its family.
    /// </summary>
    /// <remarks>
    /// The read side of the catalog rule above, and the compile-time safety this repository
    /// declined to buy from generated designer classes: a key is a string, so a typo compiles
    /// and renders the key itself at runtime — in every language at once. Each source file's
    /// localizer variables are mapped to their declared family, and every literal key asked of
    /// one is held against that family's neutral file. Lookups whose key is an expression — the
    /// funnel's <c>catalog[error.ErrorCode]</c> — carry no literal and are deliberately out of
    /// reach here; the code-keyed family is held by
    /// <see cref="EveryDomainErrorKey_IsACodeTheDomainRaises"/> instead. The reverse direction —
    /// a key nobody asks — stays out too: two families are asked dynamically, and a screen
    /// family's key may legitimately precede its screen by a commit.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0089",
        "every surface reads its words from its own family: a literal key a source file asks of " +
        "a localizer must exist in that family's neutral file, or the screen shows the key itself")]
    public void EveryKeyAScreenAsks_ExistsInItsFamily()
    {
        var neutralKeys = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        SourceTree.AllFiles
            .Select(path => (Absolute: path, Relative: SourceTree.Relative(path)))
            .Where(file => file.Relative.StartsWith("src/", StringComparison.Ordinal)
                           && (file.Relative.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                               || file.Relative.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            .Selected("source file under src/")
            .SelectMany(file => AskedKeys(file.Absolute)
                .Select(asked => (file.Relative, asked.Family, asked.Key)))
            .Where(asked => !NeutralKeysOf(neutralKeys, asked.Family).Contains(asked.Key))
            .OrderBy(asked => asked.Relative, StringComparer.Ordinal)
            .ThenBy(asked => asked.Key, StringComparer.Ordinal)
            .Select(asked =>
                $"{asked.Relative} asks {asked.Family} for '{asked.Key}', which " +
                $"{TranslationsRoot}{asked.Family}.resx does not declare: the screen would show " +
                "the key itself, in every language at once (ADR 0089)")
            .ShouldHold();
    }

    /// <summary>
    /// No notice, composes its own prose.
    /// </summary>
    /// <remarks>
    /// The presence half of ADR 0091, and the half that makes the deferral impossible to reopen:
    /// a consumer that writes its own subject and body writes them in one language, and every
    /// recipient who reads another gets English by accident rather than by decision. So the
    /// message a consumer hands the gateway carries a composed notification's two properties and
    /// nothing else — a literal cannot match the shape, which is the point. The prose itself lives
    /// where ADR 0090 put it, in the resource families the email corner of the infrastructure
    /// reads.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0091",
        "no consumer writes the words of an email: the message it sends carries a composed " +
        "notification's subject and body, and the words come from the translations")]
    public void NoNotice_ComposesItsOwnProse() =>
        EmailConsumers()
            .Selected("email consumer")
            .Select(consumer => (consumer.Relative, Match: ComposedMessage.Match(consumer.Text)))
            .Where(consumer => !consumer.Match.Success
                               || !string.Equals(
                                   consumer.Match.Groups["subject"].Value,
                                   consumer.Match.Groups["body"].Value,
                                   StringComparison.Ordinal))
            .Select(consumer =>
                $"{consumer.Relative} builds an EmailMessage whose subject and body are not one " +
                "composed notification's. A consumer that writes its own prose writes it in one " +
                "language, and every recipient who reads another gets English by accident " +
                "(ADR 0091)")
            .ShouldHold();

    /// <summary>
    /// Every notice, reads its language where it reads its address.
    /// </summary>
    /// <remarks>
    /// ADR 0091's thesis, made mechanical. An email is written in the language of the person who
    /// opens it, and the only way to be sure of that is to take the language from whatever
    /// resolved the recipient: the fact when the address rides on the fact, the invitation when
    /// the store just minted one, the read port when a port answered who to tell. Reading it
    /// anywhere else means reading the culture of whoever triggered the action — which is right
    /// for the five notices a person sends themselves and wrong for the four an administrator or
    /// a visitor causes, and a rule cannot tell the two apart. Same root, both times, is what it
    /// can tell.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0091",
        "an email is written in the language of its recipient, and that language is read from " +
        "the same source as their address — never from the culture of whoever caused it")]
    public void EveryNotice_ReadsItsLanguageWhereItReadsItsAddress() =>
        EmailConsumers()
            .Selected("email consumer")
            .Select(consumer => (
                consumer.Relative,
                Address: ComposedMessage.Match(consumer.Text).Groups["address"].Value,
                Language: ComposedLanguage.Match(consumer.Text).Groups["language"].Value))
            .Where(consumer => consumer.Language.Length == 0
                               || !string.Equals(consumer.Address, consumer.Language, StringComparison.Ordinal))
            .Select(consumer => consumer.Language.Length == 0
                ? $"{consumer.Relative} composes no notification from a Language: it cannot be " +
                  "writing in the recipient's (ADR 0091)"
                : $"{consumer.Relative} addresses '{consumer.Address}' but composes from " +
                  $"'{consumer.Language}.Language'. The language belongs to the person who opens " +
                  "the message, so it is read wherever their address was (ADR 0091)")
            .ShouldHold();

    /// <summary>Every consumer that sends an email, with its source.</summary>
    private static IReadOnlyList<(string Relative, string Text)> EmailConsumers() =>
    [
        .. SourceTree.SourceFiles
            .Select(path => (Relative: SourceTree.Relative(path), Text: SourceTree.ReadText(path)))
            .Where(file => file.Relative.StartsWith(ConsumersRoot, StringComparison.Ordinal)
                           && file.Text.Contains("new EmailMessage(", StringComparison.Ordinal))
            .OrderBy(file => file.Relative, StringComparer.Ordinal)
    ];

    /// <summary>
    /// Every topic, is named by the translations.
    /// </summary>
    /// <remarks>
    /// The census of ADR 0091's other half, in the shape
    /// <see cref="EveryDomainErrorKey_IsACodeTheDomainRaises"/> already uses and in both
    /// directions, because both failures are silent. A subject with no key renders its key on
    /// four screens, in every language at once; a key naming no subject is a translation of
    /// nothing, kept green by never being asked for. The names themselves stay English wherever
    /// they are values rather than words — the filter's query string, the contract's
    /// <c>[KnownTopic]</c>, the search index — so what a visitor reads and what a request carries
    /// are deliberately two different things.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0091",
        "the closed set of subjects is a vocabulary a visitor reads: every topic the domain " +
        "admits has a key in the topic family, and every key in it names a topic")]
    public void EveryTopic_IsNamedByTheTranslations()
    {
        var declared = Topic.GetTopics()
            .Select(topic => topic.Name)
            .Selected("topic")
            .ToDictionary(TopicKey, name => name, StringComparer.Ordinal);

        var keys = NeutralKeysOf(new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal), "TopicResources");

        declared.Keys
            .Except(keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(key =>
                $"TopicResources.resx declares no '{key}' for the topic '{declared[key]}': the " +
                "four screens that offer it would show the key itself (ADR 0091)")
            .Concat(keys
                .Except(declared.Keys, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(key =>
                    $"TopicResources.resx carries '{key}', which names no topic the domain " +
                    "admits: a translation of nothing, kept green by never being asked for " +
                    "(ADR 0091)"))
            .ShouldHold();
    }

    /// <summary>
    /// The key a topic's name is filed under: its own name in Pascal case, prefixed by the family.
    /// </summary>
    /// <remarks>
    /// Derived rather than mapped, so a topic added to the closed set names its key without a
    /// second list to keep in step — and the derivation lands on the identifier the front end
    /// already uses for the same subject, <c>DataAndAnalytics</c> for <c>Data and Analytics</c>.
    /// </remarks>
    private static string TopicKey(string name) =>
        "Topic_" + string.Concat(name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    /// <summary>
    /// No screen, shows a word it did not ask for.
    /// </summary>
    /// <remarks>
    /// The direction <see cref="EveryKeyAScreenAsks_ExistsInItsFamily"/> deliberately leaves open,
    /// and the reason ADR 0091 closes it: that rule holds a key a screen asks against its family,
    /// so a screen that asks for nothing at all satisfies it perfectly. Until this rule, every
    /// translated screen in this repository was translated by discipline — one
    /// <c>&lt;MudText&gt;Saved&lt;/MudText&gt;</c> would have shipped in English to a French
    /// visitor and no build, no request and no test would have said so.
    /// <para>
    /// What it reads is the markup alone: the <c>@code</c> block, the directives, the comments,
    /// the scripts and the styles are removed first, then every Razor transition to C# is
    /// consumed, and what remains between the tags is what a browser paints. Two things in it are
    /// not words a visitor reads, and both are named rather than guessed — an HTML entity, and a
    /// C# statement inside a control-flow block, which ends in a semicolon where prose never
    /// does.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0091",
        "every word a screen paints comes from a resource family: a literal between the tags of " +
        "a component is a sentence one language gets and the other two never will")]
    public void NoScreen_ShowsAWordItDidNotAskFor() =>
        SourceTree.AllFiles
            .Where(path => path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                           && !Path.GetFileName(path).Equals("_Imports.razor", StringComparison.Ordinal))
            .Select(path => (Relative: SourceTree.Relative(path), Absolute: path))
            .Where(file => file.Relative.StartsWith(WebRoot, StringComparison.Ordinal))
            .OrderBy(file => file.Relative, StringComparer.Ordinal)
            .Selected("component of the web front end")
            .SelectMany(file => PaintedLiterals(file.Absolute)
                .Select(literal => $"{file.Relative} paints \"{literal}\", which it asked no " +
                                   "family for: the other two languages would read it in English, " +
                                   "and nothing would report it (ADR 0091)"))
            .ShouldHold();

    /// <summary>Every run of words a component paints without asking a family for it.</summary>
    /// <remarks>
    /// The brand is the one exemption, and it is an exemption because it is not a word: a product
    /// is called what it is called in every language, and translating <c>TrainingHub</c> would be
    /// translating a name.
    /// </remarks>
    private static IEnumerable<string> PaintedLiterals(string absolutePath) =>
        TextNodes(Markup(SourceTree.ReadText(absolutePath)))
            .SelectMany(node => node.Split('\n'))
            .Where(line => !CodeFragment.IsMatch(line))
            .Select(line => HtmlEntity.Replace(line, " ").Replace(Brand, " ", StringComparison.Ordinal).Trim())
            .Where(line => Word.IsMatch(line));

    /// <summary>What a browser is asked to paint: the file, minus everything that is not markup.</summary>
    private static string Markup(string razor)
    {
        var code = CodeBlock.Match(razor);
        var markup = code.Success ? razor[..code.Index] : razor;

        markup = RazorComment.Replace(markup, " ");
        markup = HtmlComment.Replace(markup, " ");
        markup = ScriptOrStyle.Replace(markup, " ");

        return Directive.Replace(markup, " ");
    }

    /// <summary>
    /// The text between the tags, with every Razor transition to C# consumed.
    /// </summary>
    /// <remarks>
    /// Hand-walked rather than matched, because a transition ends where its brackets balance and
    /// no regular expression counts brackets. Everything a tag holds is skipped whole — an
    /// attribute's value is not a text node, and the one attribute a person reads,
    /// <c>aria-label</c>, is already held by <see cref="EveryKeyAScreenAsks_ExistsInItsFamily"/>
    /// through the localizer it has to name.
    /// </remarks>
    private static IEnumerable<string> TextNodes(string markup)
    {
        var nodes = new List<string>();
        var index = 0;

        while (index < markup.Length)
        {
            if (markup[index] == '<')
            {
                index = EndOfTag(markup, index);
                continue;
            }

            var node = new StringBuilder();

            while (index < markup.Length && markup[index] != '<')
            {
                if (markup[index] != '@')
                {
                    node.Append(markup[index++]);
                    continue;
                }

                // "@@" is how Razor writes a literal at sign, and it paints one character.
                index = index + 1 < markup.Length && markup[index + 1] == '@'
                    ? index + 2
                    : EndOfTransition(markup, index);
            }

            nodes.Add(node.ToString());
        }

        return nodes;
    }

    /// <summary>The index just past a tag, quoted attribute values included.</summary>
    private static int EndOfTag(string markup, int index)
    {
        while (index < markup.Length && markup[index] != '>')
        {
            if (markup[index] == '"')
            {
                index++;

                while (index < markup.Length && markup[index] != '"')
                {
                    index++;
                }
            }

            index++;
        }

        return index + 1;
    }

    /// <summary>The index just past a Razor transition to C#, whatever shape it takes.</summary>
    private static int EndOfTransition(string markup, int index)
    {
        var start = index + 1;

        if (start < markup.Length && markup[start] is '{' or '(')
        {
            return Balanced(markup, start);
        }

        var keyword = Keyword.Match(markup[start..]);

        if (keyword.Success)
        {
            var afterKeyword = SkipWhitespace(markup, start + keyword.Length);

            return afterKeyword < markup.Length && markup[afterKeyword] == '('
                ? Balanced(markup, afterKeyword)
                : start + keyword.Length;
        }

        var cursor = start;

        while (cursor < markup.Length && (char.IsLetterOrDigit(markup[cursor]) || markup[cursor] == '_'))
        {
            cursor++;
        }

        while (cursor < markup.Length)
        {
            if (markup[cursor] is '(' or '[')
            {
                cursor = Balanced(markup, cursor);
            }
            else if (markup[cursor] == '?' && cursor + 1 < markup.Length && markup[cursor + 1] == '.')
            {
                cursor++;
            }
            else if (markup[cursor] == '.'
                     && cursor + 1 < markup.Length
                     && (char.IsLetter(markup[cursor + 1]) || markup[cursor + 1] == '_'))
            {
                cursor++;

                while (cursor < markup.Length && (char.IsLetterOrDigit(markup[cursor]) || markup[cursor] == '_'))
                {
                    cursor++;
                }
            }
            else
            {
                break;
            }
        }

        return cursor;
    }

    /// <summary>The index just past the bracket opened at <paramref name="index"/>.</summary>
    private static int Balanced(string markup, int index)
    {
        var depth = 0;

        while (index < markup.Length)
        {
            if (markup[index] is '(' or '[' or '{')
            {
                depth++;
            }
            else if (markup[index] is ')' or ']' or '}')
            {
                depth--;

                if (depth == 0)
                {
                    return index + 1;
                }
            }

            index++;
        }

        return markup.Length;
    }

    private static int SkipWhitespace(string markup, int index)
    {
        while (index < markup.Length && char.IsWhiteSpace(markup[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>Every (family, key) pair a source file asks of a localizer by literal.</summary>
    /// <remarks>
    /// The map is built from the file's own declarations — a Razor <c>@inject</c> and a C#
    /// parameter or pattern write the same <c>IStringLocalizer&lt;Family&gt; name</c> shape — so
    /// only variables the file binds to a family are followed, and an indexer on anything else
    /// never becomes a false positive.
    /// </remarks>
    private static IEnumerable<(string Family, string Key)> AskedKeys(string absolutePath)
    {
        var text = SourceTree.ReadText(absolutePath);

        var declared = LocalizerDeclaration.Matches(text)
            .GroupBy(match => match.Groups["name"].Value, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Groups["family"].Value,
                StringComparer.Ordinal);

        if (declared.Count == 0)
        {
            return [];
        }

        return LocalizerLookup.Matches(text)
            .Where(match => declared.ContainsKey(match.Groups["name"].Value))
            .Select(match => (declared[match.Groups["name"].Value], match.Groups["key"].Value));
    }

    /// <summary>The neutral keys of a family, read once per family for the whole scan.</summary>
    private static IReadOnlySet<string> NeutralKeysOf(
        Dictionary<string, IReadOnlySet<string>> cache, string family)
    {
        if (!cache.TryGetValue(family, out var keys))
        {
            var neutral = Path.Combine(
                SourceTree.RepositoryRoot, "src", "TrainingHub.Translations", family + ".resx");

            // A family with no neutral file declares no keys, so every literal asked of it is a
            // violation naming the missing file's path.
            keys = File.Exists(neutral) ? Keys(neutral) : new HashSet<string>(StringComparer.Ordinal);
            cache[family] = keys;
        }

        return keys;
    }

    /// <summary>The family a resource file belongs to: its name up to the culture, if any.</summary>
    private static string FamilyOf(string path)
    {
        var withoutFormat = Path.GetFileNameWithoutExtension(path);
        var culture = Path.GetExtension(withoutFormat);

        return culture.Length == 0 ? withoutFormat : Path.GetFileNameWithoutExtension(withoutFormat);
    }

    /// <summary>The culture segment of a resource file's name — <c>fr</c> — or empty for the neutral file.</summary>
    private static string CultureOf(string path)
    {
        var culture = Path.GetExtension(Path.GetFileNameWithoutExtension(path));

        return culture.Length == 0 ? string.Empty : culture[1..];
    }

    /// <summary>The keys a resource file declares, read from its XML rather than through a build.</summary>
    private static IReadOnlySet<string> Keys(string absolutePath) =>
        XDocument.Parse(SourceTree.ReadText(absolutePath))
            .Root!
            .Elements("data")
            .Select(data => (string?)data.Attribute("name"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"IStringLocalizer<(?<family>\w+)>\s+(?<name>\w+)")]
    private static partial Regex LocalizerDeclaration { get; }

    [GeneratedRegex(@"(?<name>\w+)\[""(?<key>[^""]+)""")]
    private static partial Regex LocalizerLookup { get; }

    [GeneratedRegex(@"new EmailMessage\(\s*(?<address>\w+)\.\w+,\s*(?<subject>\w+)\.Subject,\s*(?<body>\w+)\.Body")]
    private static partial Regex ComposedMessage { get; }

    [GeneratedRegex(@"composer\.\w+\(\s*(?<language>\w+)\.Language\b")]
    private static partial Regex ComposedLanguage { get; }

    [GeneratedRegex(@"^@code\b", RegexOptions.Multiline)]
    private static partial Regex CodeBlock { get; }

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex RazorComment { get; }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlComment { get; }

    [GeneratedRegex(@"<(?<tag>script|style)\b.*?</\k<tag>>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOrStyle { get; }

    [GeneratedRegex(
        @"^[ \t]*@(page|using|inject|inherits|implements|attribute|namespace|typeparam|layout|rendermode|preservewhitespace)\b.*$",
        RegexOptions.Multiline)]
    private static partial Regex Directive { get; }

    [GeneratedRegex(@"^(if|else|foreach|for|while|switch|do|try|catch|finally|lock|using)\b")]
    private static partial Regex Keyword { get; }

    /// <summary>
    /// A line of a text node that is not prose: markup punctuation, or a C# statement.
    /// </summary>
    /// <remarks>
    /// A control-flow block's body is C# that happens to hold markup, so a statement standing
    /// between two elements — <c>var topic = facet.Topic;</c> inside a <c>@foreach</c> — reaches
    /// the scan as text. It ends in a semicolon, and prose never does.
    /// </remarks>
    [GeneratedRegex(@"^\s*([{}]+|(else|case|default)\b.*|.*;)\s*$")]
    private static partial Regex CodeFragment { get; }

    [GeneratedRegex(@"&[a-zA-Z]+;|&#\d+;")]
    private static partial Regex HtmlEntity { get; }

    [GeneratedRegex(@"\p{L}{2,}")]
    private static partial Regex Word { get; }

    [GeneratedRegex(@"All\s*=\s*\[(?<items>[^\]]*)\]")]
    private static partial Regex DeclaredList { get; }

    [GeneratedRegex(@"Default\s*=\s*""(?<code>[a-zA-Z-]+)""")]
    private static partial Regex DeclaredDefaultCode { get; }

    [GeneratedRegex(@"""(?<code>[a-zA-Z-]+)""")]
    private static partial Regex QuotedCode { get; }

    /// <summary>The one list every surface shares, read from its source like the Program.cs pins.</summary>
    private static string SupportedLanguagesSource() =>
        SourceTree.ReadText(Path.Combine(
            SourceTree.RepositoryRoot, "src", "TrainingHub.Translations", "SupportedLanguages.cs"));

    /// <summary>Every language <c>SupportedLanguages.All</c> declares.</summary>
    private static IReadOnlyList<string> DeclaredLanguages() =>
        [.. QuotedCode.Matches(DeclaredList.Match(SupportedLanguagesSource()).Groups["items"].Value)
            .Select(match => match.Groups["code"].Value)];

    /// <summary>The language <c>SupportedLanguages.Default</c> declares.</summary>
    private static string DeclaredDefault() =>
        DeclaredDefaultCode.Match(SupportedLanguagesSource()).Groups["code"].Value;
}
