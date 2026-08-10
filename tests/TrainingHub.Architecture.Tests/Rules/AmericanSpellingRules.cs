using System.Globalization;
using System.Text.RegularExpressions;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The one spelling this repository writes, held to (ADR 0064).
/// </summary>
/// <remarks>
/// A convention nothing checks is a preference, and a preference drifts the first time somebody
/// types the word they grew up with. This repository had drifted before the record: <c>catalogue</c>
/// and <c>catalog</c> both named the same thing, one in a helper class and one in the domain's own
/// vocabulary, and nothing said which was meant.
/// <para>
/// Prose as well as code, and that is the point rather than an excess. The words that go wrong are
/// mostly in comments and records — a type is read by a compiler, a sentence is read by whoever
/// arrives next, and a repository whose sentences change dialect between files is one that has to be
/// read twice.
/// </para>
/// </remarks>
public sealed partial class AmericanSpellingRules
{
    /// <summary>
    /// The British spellings this repository refuses, and what it writes instead.
    /// </summary>
    /// <remarks>
    /// A pinned list rather than a dictionary package, for the reason ADR 0064 gives: a rule that
    /// pulls a word list at build time is a rule whose meaning changes when somebody else ships a
    /// release. What is here is what this repository has met or expects to meet; a word nobody has
    /// written yet costs nothing to carry and catches the day somebody writes it.
    /// <para>
    /// Words spelt the same on both sides are deliberately absent, and <c>analysis</c> is the one to
    /// know about: only the verb moves — <em>analyse</em> becomes <em>analyze</em> — while the noun
    /// is already American. A list that folded them together would rename
    /// <c>AnalysisRules</c> and be wrong.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> BritishToAmerican =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // -ise, -isation, -iser  ->  -ize, -ization, -izer
            ["ORGANISE"] = "organize",
            ["ORGANISES"] = "organizes",
            ["ORGANISED"] = "organized",
            ["ORGANISING"] = "organizing",
            ["ORGANISATION"] = "organization",
            ["ORGANISATIONS"] = "organizations",
            ["AUTHORISE"] = "authorize",
            ["AUTHORISES"] = "authorizes",
            ["AUTHORISED"] = "authorized",
            ["AUTHORISING"] = "authorizing",
            ["AUTHORISATION"] = "authorization",
            ["NORMALISE"] = "normalize",
            ["NORMALISES"] = "normalizes",
            ["NORMALISED"] = "normalized",
            ["NORMALISING"] = "normalizing",
            ["NORMALISATION"] = "normalization",
            ["RECOGNISE"] = "recognize",
            ["RECOGNISES"] = "recognizes",
            ["RECOGNISED"] = "recognized",
            ["RECOGNISING"] = "recognizing",
            ["INITIALISE"] = "initialize",
            ["INITIALISES"] = "initializes",
            ["INITIALISED"] = "initialized",
            ["INITIALISING"] = "initializing",
            ["INITIALISER"] = "initializer",
            ["INITIALISERS"] = "initializers",
            ["INITIALISATION"] = "initialization",
            ["SERIALISE"] = "serialize",
            ["SERIALISES"] = "serializes",
            ["SERIALISED"] = "serialized",
            ["SERIALISING"] = "serializing",
            ["SERIALISER"] = "serializer",
            ["SERIALISATION"] = "serialization",
            ["SANITISE"] = "sanitize",
            ["SANITISES"] = "sanitizes",
            ["SANITISED"] = "sanitized",
            ["SANITISING"] = "sanitizing",
            ["SANITISER"] = "sanitizer",
            ["SANITISERS"] = "sanitizers",
            ["SANITISATION"] = "sanitization",
            ["CATEGORISE"] = "categorize",
            ["CATEGORISED"] = "categorized",
            ["PRIORITISE"] = "prioritize",
            ["PRIORITISED"] = "prioritized",
            ["SUMMARISE"] = "summarize",
            ["SUMMARISES"] = "summarizes",
            ["SUMMARISED"] = "summarized",
            ["MINIMISE"] = "minimize",
            ["MINIMISED"] = "minimized",
            ["MAXIMISE"] = "maximize",
            ["MAXIMISED"] = "maximized",
            ["REALISE"] = "realize",
            ["REALISES"] = "realizes",
            ["REALISED"] = "realized",
            ["UTILISE"] = "utilize",
            ["UTILISED"] = "utilized",
            ["SPECIALISE"] = "specialize",
            ["SPECIALISED"] = "specialized",
            ["STANDARDISE"] = "standardize",
            ["STANDARDISED"] = "standardized",
            ["CUSTOMISE"] = "customize",
            ["CUSTOMISED"] = "customized",
            ["OPTIMISE"] = "optimize",
            ["OPTIMISED"] = "optimized",
            ["OPTIMISATION"] = "optimization",
            ["OPTIMISATIONS"] = "optimizations",
            ["SYNCHRONISE"] = "synchronize",
            ["SYNCHRONISED"] = "synchronized",
            ["TOKENISE"] = "tokenize",
            ["TOKENISED"] = "tokenized",
            ["TOKENISATION"] = "tokenization",
            ["MATERIALISE"] = "materialize",
            ["MATERIALISES"] = "materializes",
            ["MATERIALISED"] = "materialized",
            ["MATERIALISING"] = "materializing",
            ["VISUALISE"] = "visualize",
            ["VISUALISED"] = "visualized",
            ["CHARACTERISE"] = "characterize",
            ["CHARACTERISES"] = "characterizes",
            ["CHARACTERISED"] = "characterized",
            ["EMPHASISE"] = "emphasize",
            ["EMPHASISED"] = "emphasized",
            ["CRITICISE"] = "criticize",
            ["CRITICISED"] = "criticized",
            ["FINALISE"] = "finalize",
            ["FINALISED"] = "finalized",
            ["FORMALISE"] = "formalize",
            ["FORMALISED"] = "formalized",
            ["GENERALISE"] = "generalize",
            ["GENERALISES"] = "generalizes",
            ["GENERALISED"] = "generalized",
            ["LOCALISE"] = "localize",
            ["LOCALISED"] = "localized",
            ["MODERNISE"] = "modernize",
            ["MODERNISED"] = "modernized",
            ["MODERNISATION"] = "modernization",
            ["NEUTRALISE"] = "neutralize",
            ["NEUTRALISED"] = "neutralized",
            ["RANDOMISE"] = "randomize",
            ["RANDOMISED"] = "randomized",
            ["HARMONISE"] = "harmonize",
            ["HARMONISES"] = "harmonizes",
            ["HARMONISED"] = "harmonized",
            ["HARMONISING"] = "harmonizing",
            ["HARMONISATION"] = "harmonization",
            ["APOLOGISE"] = "apologize",
            ["MEMORISE"] = "memorize",
            ["PUBLICISE"] = "publicize",

            // -yse -> -yze. The verb only; "analysis" is already American.
            ["ANALYSE"] = "analyze",
            ["ANALYSES"] = "analyzes",
            ["ANALYSED"] = "analyzed",
            ["ANALYSING"] = "analyzing",
            ["ANALYSER"] = "analyzer",
            ["ANALYSERS"] = "analyzers",
            ["PARALYSE"] = "paralyze",
            ["PARALYSED"] = "paralyzed",
            ["CATALYSE"] = "catalyze",
            ["CATALYSED"] = "catalyzed",

            // -our -> -or
            ["COLOUR"] = "color",
            ["COLOURS"] = "colors",
            ["COLOURED"] = "colored",
            ["COLOURFUL"] = "colorful",
            ["BEHAVIOUR"] = "behavior",
            ["BEHAVIOURS"] = "behaviors",
            ["BEHAVIOURAL"] = "behavioral",
            ["BEHAVIOURALLY"] = "behaviorally",
            ["FAVOUR"] = "favor",
            ["FAVOURS"] = "favors",
            ["FAVOURED"] = "favored",
            ["FAVOURABLE"] = "favorable",
            ["FAVOURITE"] = "favorite",
            ["FAVOURITES"] = "favorites",
            ["HONOUR"] = "honor",
            ["HONOURS"] = "honors",
            ["HONOURED"] = "honored",
            ["HONOURING"] = "honoring",
            ["LABOUR"] = "labor",
            ["LABOURS"] = "labors",
            ["LABOURED"] = "labored",
            ["NEIGHBOUR"] = "neighbor",
            ["NEIGHBOURS"] = "neighbors",
            ["NEIGHBOURING"] = "neighboring",
            ["RUMOUR"] = "rumor",
            ["HUMOUR"] = "humor",
            ["ENDEAVOUR"] = "endeavor",
            ["FLAVOUR"] = "flavor",
            ["FLAVOURS"] = "flavors",
            ["HARBOUR"] = "harbor",
            ["VAPOUR"] = "vapor",
            ["SAVOUR"] = "savor",
            ["VIGOUR"] = "vigor",
            ["VALOUR"] = "valor",
            ["ARMOUR"] = "armor",
            ["ODOUR"] = "odor",
            ["SPLENDOUR"] = "splendor",
            ["PARLOUR"] = "parlor",
            ["RIGOUR"] = "rigor",
            ["ARDOUR"] = "ardor",

            // -re -> -er
            ["CENTRE"] = "center",
            ["CENTRES"] = "centers",
            ["CENTRED"] = "centered",
            ["METRE"] = "meter",
            ["METRES"] = "meters",
            ["LITRE"] = "liter",
            ["LITRES"] = "liters",
            ["THEATRE"] = "theater",
            ["FIBRE"] = "fiber",
            ["FIBRES"] = "fibers",
            ["CALIBRE"] = "caliber",
            ["LUSTRE"] = "luster",
            ["SOMBRE"] = "somber",
            ["SPECTRE"] = "specter",
            ["MANOEUVRE"] = "maneuver",

            // -ce -> -se
            ["LICENCE"] = "license",
            ["LICENCES"] = "licenses",
            ["DEFENCE"] = "defense",
            ["DEFENCES"] = "defenses",
            ["OFFENCE"] = "offense",
            ["OFFENCES"] = "offenses",
            ["PRETENCE"] = "pretense",
            ["PRACTISE"] = "practice",
            ["PRACTISES"] = "practices",
            ["PRACTISED"] = "practiced",
            ["PRACTISING"] = "practicing",

            // -ogue -> -og
            ["CATALOGUE"] = "catalog",
            ["CATALOGUES"] = "catalogs",
            ["CATALOGUED"] = "cataloged",
            ["DIALOGUE"] = "dialog",
            ["DIALOGUES"] = "dialogs",
            ["ANALOGUE"] = "analog",
            ["ANALOGUES"] = "analogs",

            // A consonant this side of the Atlantic does not double before a suffix.
            ["CANCELLED"] = "canceled",
            ["CANCELLING"] = "canceling",
            ["TRAVELLED"] = "traveled",
            ["TRAVELLING"] = "traveling",
            ["LABELLED"] = "labeled",
            ["LABELLING"] = "labeling",
            ["MODELLED"] = "modeled",
            ["MODELLING"] = "modeling",
            ["SIGNALLED"] = "signaled",
            ["SIGNALLING"] = "signaling",
            ["MARVELLOUS"] = "marvelous",
            ["SKILFUL"] = "skillful",
            ["COUNSELLOR"] = "counselor",

            // …and one it does double.
            ["ENROLMENT"] = "enrollment",
            ["ENROLMENTS"] = "enrollments",
            ["FULFIL"] = "fulfill",
            ["FULFILS"] = "fulfills",
            ["FULFILMENT"] = "fulfillment",
            ["INSTALMENT"] = "installment",
            ["INSTALMENTS"] = "installments",

            // The rest, alphabetically.
            ["ACKNOWLEDGEMENT"] = "acknowledgment",
            ["ACKNOWLEDGEMENTS"] = "acknowledgments",
            ["AGEING"] = "aging",
            ["ALUMINIUM"] = "aluminum",
            ["AMONGST"] = "among",
            ["ARTEFACT"] = "artifact",
            ["ARTEFACTS"] = "artifacts",
            ["BURNT"] = "burned",
            ["CHEQUE"] = "check",
            ["COSY"] = "cozy",
            ["DRAUGHT"] = "draft",
            ["DREAMT"] = "dreamed",
            ["GREY"] = "gray",
            ["GREYED"] = "grayed",
            ["JUDGEMENT"] = "judgment",
            ["JUDGEMENTS"] = "judgments",
            ["KERB"] = "curb",
            ["LEARNT"] = "learned",
            ["MOULD"] = "mold",
            ["MOULDS"] = "molds",
            ["PLOUGH"] = "plow",
            ["PROGRAMME"] = "program",
            ["PROGRAMMES"] = "programs",
            ["SCEPTIC"] = "skeptic",
            ["SCEPTICAL"] = "skeptical",
            ["SMOULDER"] = "smolder",
            ["SPELT"] = "spelled",
            ["SPOILT"] = "spoiled",
            ["STOREY"] = "story",
            ["SULPHUR"] = "sulfur",
            ["TYRE"] = "tire",
            ["WHILST"] = "while",
        };

    /// <summary>
    /// The extensions this rule reads.
    /// </summary>
    /// <remarks>
    /// Declared rather than derived, because the alternative is reading a PNG as text and finding a
    /// word in the noise. Everything this repository <em>writes</em> is here; everything it
    /// <em>stores</em> is not.
    /// </remarks>
    private static readonly IReadOnlySet<string> WrittenExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".razor", ".css", ".js", ".md", ".json", ".yml", ".yaml",
            ".csproj", ".props", ".slnx", ".sh", ".nswag", ".http", ".sql",
            ".editorconfig", ".gitignore", ".dockerignore"
        };

    /// <summary>The file that declares the list above, and therefore has to hold every word in it.</summary>
    private const string ThisRule = "tests/TrainingHub.Architecture.Tests/Rules/AmericanSpellingRules.cs";

    /// <summary>
    /// The first record written under this convention. Everything below it is history.
    /// </summary>
    /// <remarks>
    /// A merged record is never rewritten (ADR 0039, ADR 0040): what it describes is what was
    /// decided on its date, in the words used then, and quoted verbatim by the records that came
    /// after. Rewriting sixty-three of them to move a vowel would edit the account of decisions
    /// nobody is revisiting — so this convention reaches the records written from 0064 onwards, and
    /// says so rather than leaving the gap to be discovered.
    /// </remarks>
    private const int FirstRecordUnderThisConvention = 64;

    /// <summary>
    /// Every word this repository writes, uses American spelling.
    /// </summary>
    /// <remarks>
    /// Both halves of an identifier are checked, because a camel-cased name hides its words from a
    /// whole-word search: <c>SkiaSharpPhotoSanitiser</c> contains no token equal to
    /// <c>sanitiser</c> until it is split.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0064",
        "American English is this repository's spelling, in identifiers, in prose and in the records alike")]
    public void EveryWordThisRepositoryWrites_UsesAmericanSpelling() =>
        GovernedFiles()
            .Selected("written file")
            .SelectMany(Offences)
            .ShouldHold();

    /// <summary>The files this convention reaches.</summary>
    private static IEnumerable<string> GovernedFiles() =>
        SourceTree.AllFiles
            .Where(path => WrittenExtensions.Contains(Path.GetExtension(path)))
            .Where(path => !SourceTree.IsGenerated(path))
            .Where(path => SourceTree.Relative(path) != ThisRule)
            .Where(path => !IsRecordFromBeforeThisConvention(SourceTree.Relative(path)));

    /// <summary>Every British word in a file, with the line it sits on.</summary>
    private static IEnumerable<string> Offences(string path)
    {
        var relative = SourceTree.Relative(path);
        var isIndex = relative == "docs/adr/README.md";
        var lines = SourceTree.ReadLines(path);

        for (var number = 1; number <= lines.Length; number++)
        {
            var line = lines[number - 1];

            // The index quotes the title of every record, including the sixty-three this convention
            // does not reach. Skipping the row rather than the file keeps the rows added after it
            // governed.
            if (isIndex && QuotesARecordFromBeforeThisConvention(line))
            {
                continue;
            }

            foreach (var (written, folded) in Words(line))
            {
                if (BritishToAmerican.TryGetValue(folded, out var american))
                {
                    yield return
                        $"'{relative}' line {number} writes '{written}'. This repository writes " +
                        $"'{american}': American English is the spelling everywhere, in an " +
                        "identifier as much as in a sentence (ADR 0064)";
                }
            }
        }
    }

    /// <summary>The words on a line, camel-cased identifiers taken apart and folded to one case.</summary>
    /// <remarks>
    /// Upper rather than lower, and the dictionary above is keyed the same way, because CA1308 is an
    /// error here: lower-casing loses information in a handful of alphabets and the analyser refuses
    /// it on principle. The comparison is between two folded strings either way.
    /// </remarks>
    private static IEnumerable<(string Written, string Folded)> Words(string line) =>
        Letters()
            .Matches(line)
            .SelectMany(match => CamelParts().Matches(match.Value).Select(part => part.Value))
            .Select(part => (part, part.ToUpperInvariant()));

    private static bool IsRecordFromBeforeThisConvention(string relative) =>
        RecordNumber(relative) is { } number && number < FirstRecordUnderThisConvention;

    private static bool QuotesARecordFromBeforeThisConvention(string line) =>
        IndexRowLink().Matches(line)
            .Select(match => int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture))
            .Any(number => number < FirstRecordUnderThisConvention);

    /// <summary>The number of a record, or nothing when the path is not one.</summary>
    private static int? RecordNumber(string relative) =>
        RecordFile().Match(relative) is { Success: true } match
            ? int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture)
            : null;

    [GeneratedRegex(@"[A-Za-z]+")]
    private static partial Regex Letters();

    [GeneratedRegex(@"[A-Z]+(?![a-z])|[A-Z][a-z]*|[a-z]+")]
    private static partial Regex CamelParts();

    [GeneratedRegex(@"^docs/adr/(?<number>\d{4})-")]
    private static partial Regex RecordFile();

    [GeneratedRegex(@"\((?<number>\d{4})-[^)]*\.md\)")]
    private static partial Regex IndexRowLink();
}
