using AwesomeAssertions;
using TrainingHub.Architecture.Tests.Framework;
using NetArchTest.Rules;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// What ADR 0063 claims about the bytes of a portrait, held to.
/// </summary>
/// <remarks>
/// Two claims, and neither is about the stripping itself — that is what the sanitizer's own tests
/// are for. These are about the two ways the stripping stops being worth anything: a second place
/// learning to decode an image, and a reader publishing a portrait without asking whether anything
/// was ever stripped from it. Both would be written in good faith, and neither would fail a test.
/// </remarks>
public sealed class PhotoSanitizationRules
{
    // The namespace rather than the package name, for the reason ObjectStorageRules gives about
    // AWSSDK: NetArchTest matches what types are declared in, and a rule written against a package
    // that declares a differently named namespace matches nothing and passes everywhere.
    private const string ImageCodecNamespace = "SkiaSharp";

    /// <summary>The adapter that owns the write side's half of ADR 0063.</summary>
    private const string CatalogReader = "src/TrainingHub.Shared.Infrastructure/Search/CatalogDetailQuery.cs";

    /// <summary>The stamp a publishable portrait carries, named as the reader must name it.</summary>
    private const string SanitizationStamp = "SanitizedOnUtc";

    /// <summary>
    /// Only the infrastructure, decodes an image.
    /// </summary>
    /// <remarks>
    /// The same shape as <c>OnlyTheInfrastructure_KnowsTheObjectStore</c> and
    /// <c>OnlyTheInfrastructure_SpeaksSmtp</c>, and for a sharper reason than either. An image codec
    /// is not merely a dependency to keep swappable: it is the largest attack surface this product
    /// has, since it parses a file a stranger uploaded. One place decoding one is a place that can
    /// be reviewed; a second is how a domain object or a controller quietly starts reading pixels
    /// on a request thread.
    /// <para>
    /// Backend rather than everything, exactly as the object-store rule is, and for its reason: the
    /// adapter's own tests decode images because the decoding is the subject, and a rule that
    /// forbade the one test proving the orientation fact would be answered by deleting it.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0063",
        "one project decodes an image, so the parser reading a stranger's upload is a place that can be reviewed")]
    public void OnlyTheInfrastructure_DecodesAnImage()
    {
        var everywhereElse = Solution.Backend
            .Where(assembly => assembly != Solution.Infrastructure)
            .ToArray();

        foreach (var assembly in everywhereElse)
        {
            Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOnAny(ImageCodecNamespace)
                .GetResult()
                .ShouldHold();
        }
    }

    /// <summary>
    /// No unsanitized portrait, is published.
    /// </summary>
    /// <remarks>
    /// The precondition ADR 0062 named, made executable. What the public catalog serves is bytes
    /// belonging to somebody who never agreed to publish where they were standing, and the only
    /// thing standing between the two is a column the reader has to consult.
    /// <para>
    /// Written against the reader's source rather than against its behavior, and that is a
    /// deliberate second layer rather than a shortcut. The SQLite facts already prove the current
    /// reader refuses an unstamped photo; what they cannot prove is that a <em>next</em> reader —
    /// a fourth method on this port, added in good faith by somebody who never read this record —
    /// remembers to ask. The stamp is not something a query naturally reaches for, so its absence
    /// from this file is the signal.
    /// </para>
    /// <para>
    /// One file rather than a scan, because the sharing of authority is the point: this is the only
    /// adapter allowed to open those tables (ADR 0059, ADR 0062), so it is the only place a portrait
    /// can be published from. A rule ranging wider would be satisfied by any file mentioning the
    /// column and would stop saying anything.
    /// </para>
    /// </remarks>
    [Fact]
    [ArchitectureRule("0063",
        "what nothing can prove was stripped is not published, so the reader that serves a portrait consults the stamp")]
    public void NoUnsanitizedPortrait_IsPublished()
    {
        var path = Path.Combine(SourceTree.RepositoryRoot, CatalogReader);

        File.Exists(path).Should().BeTrue(
            $"'{CatalogReader}' is the adapter this rule is about, and a rule that cannot find " +
            "its subject passes by selecting nothing");

        var occurrences = SourceTree
            .ReadLines(path)
            .Count(line => line.Contains(SanitizationStamp, StringComparison.Ordinal)
                && !line.TrimStart().StartsWith("//", StringComparison.Ordinal));

        occurrences.Should().BeGreaterThan(0,
            $"'{CatalogReader}' publishes a trainer's portrait to anybody and never names " +
            $"'{SanitizationStamp}'. A portrait stored before ADR 0063 carries whatever the camera " +
            "wrote, GPS included, and nothing but this predicate keeps it off the public catalog");
    }
}
