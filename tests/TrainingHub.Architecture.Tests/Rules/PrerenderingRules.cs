using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// Which routes prerender, who decides (ADR 0072), and what the prerendered pages say about
/// themselves (ADR 0073).
/// </summary>
/// <remarks>
/// The decision lived for a long time as a comment in <c>App.razor</c> saying it was not made.
/// Now that it is, the shape worth defending is its narrowness: the catalog's routes — the public
/// face a crawler is meant to read — prerender, and nothing else does, because everything else is
/// interactive controls behind a sign-in that a prerendered pass renders inert.
/// </remarks>
public sealed class PrerenderingRules
{
    /// <summary>
    /// The one file that decides, per request, what prerenders.
    /// </summary>
    private const string TheDecidingFile =
        "src/Web/TrainingHub.Blazor/TrainingHub.Blazor/Components/App.razor";

    /// <summary>
    /// The pages a decision could quietly move into.
    /// </summary>
    private const string TheClientsPages = "src/Web/TrainingHub.Blazor/TrainingHub.Blazor.Client/";

    /// <summary>
    /// The prerendered routes, are exactly the catalogs.
    /// </summary>
    /// <remarks>
    /// Two halves that can fail separately. The first reads the deciding file: the render mode
    /// must be computed from the request's path, keyed to the catalog's prefix and nothing
    /// broader — a hard-coded <c>prerender: true</c> would prerender the login form, which is the
    /// half of the old objection that still stands. The second sweeps the client's pages for a
    /// <c>@rendermode</c> of their own, because a page that declares one has taken the decision
    /// out of the one place this rule reads.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0072",
        "the catalog's routes prerender for the crawler ADR 0062 invited, and nothing else does: " +
        "one file decides, per request path, and no page decides for itself")]
    public void ThePrerenderedRoutes_AreExactlyTheCatalogs()
    {
        var app = SourceTree.ReadText(Path.Combine(
            SourceTree.RepositoryRoot, TheDecidingFile.Replace('/', Path.DirectorySeparatorChar)));

        new[] { "prerender: IsCatalogRoute", "\"/catalog\"", "\"/catalog/\"" }
            .Selected("shape the deciding file must keep")
            .Where(shape => !app.Contains(shape, StringComparison.Ordinal))
            .Select(shape =>
                $"'{TheDecidingFile}' no longer contains '{shape}'. The prerender decision is " +
                "per request, keyed to the catalog's routes and to nothing broader (ADR 0072)")
            .ShouldHold();

        new[] { "prerender: true", "prerender:true" }
            .Selected("shape of an unconditional prerender")
            .Where(shape => app.Contains(shape, StringComparison.Ordinal))
            .Select(shape =>
                $"'{TheDecidingFile}' contains '{shape}'. Prerendering everything renders the " +
                "signed-in screens inert until WebAssembly boots — the half of the old objection " +
                "that still stands (ADR 0072)")
            .ShouldHold();

        SourceTree.AllFiles
            .Where(file => file.EndsWith(".razor", StringComparison.Ordinal))
            .Select(SourceTree.Relative)
            .Where(file => file.StartsWith(TheClientsPages, StringComparison.Ordinal))
            .Selected("client component")
            .Where(file => SourceTree.ReadText(Path.Combine(
                    SourceTree.RepositoryRoot, file.Replace('/', Path.DirectorySeparatorChar)))
                .Contains("@rendermode", StringComparison.Ordinal))
            .Select(file =>
                $"'{file}' declares a @rendermode of its own. The decision belongs to " +
                $"'{TheDecidingFile}', per request, or the closed set of prerendered routes " +
                "stops being checkable (ADR 0072)")
            .ShouldHold();
    }

    /// <summary>
    /// Every prerendered page, describes itself to the crawler.
    /// </summary>
    /// <remarks>
    /// The point of prerendering was never the HTML alone: it was giving the machines that read
    /// the catalog something to read. A page that prerenders without a description has an engine
    /// invent its snippet, and one without a canonical has every <c>?sort=</c> view compete with
    /// the page it is a view of. The check is textual and per page, because the head is markup a
    /// refactor can quietly drop while every behavior test stays green.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0073",
        "each prerendered page describes itself: a head with a description for a search result " +
        "to quote and a canonical naming which address is the page")]
    public void EveryPrerenderedPage_DescribesItselfToTheCrawler()
    {
        string[] pages = ["Catalog.razor", "CatalogTraining.razor", "TrainerProfile.razor"];
        string[] shapes = ["<HeadContent>", "name=\"description\"", "rel=\"canonical\""];

        pages
            .SelectMany(page => shapes.Select(shape => (Page: page, Shape: shape)))
            .Selected("head shape a prerendered page must carry")
            .Where(claim => !SourceTree.ReadText(Path.Combine(
                    SourceTree.RepositoryRoot,
                    $"{TheClientsPages}Pages/Catalog/{claim.Page}"
                        .Replace('/', Path.DirectorySeparatorChar)))
                .Contains(claim.Shape, StringComparison.Ordinal))
            .Select(claim =>
                $"'{claim.Page}' no longer contains '{claim.Shape}'. A prerendered page " +
                "describes itself to the crawler it is prerendered for, or the machines reading " +
                "the catalog are back to inventing its description (ADR 0073)")
            .ShouldHold();
    }

    /// <summary>
    /// The front door, is the catalog.
    /// </summary>
    /// <remarks>
    /// Three shapes in three files, each a half of the same decision: the catalog page answers
    /// the root as well as its own address, the deciding file keys the root into the prerendered
    /// set, and the shared mapping's fallback is the newest order — so a visitor's first address
    /// is the shelf, youngest first. Any one of them quietly dropped would leave the front door
    /// half open: a root that boots empty, or a landing that opens on the alphabet.
    /// </remarks>
    [Fact]
    [ArchitectureRule("0074",
        "the root serves the catalog itself, prerendered, and the bare address answers newest " +
        "first — the front door shows what recently went on offer")]
    public void TheFrontDoor_IsTheCatalog()
    {
        const string TheCatalogPage = "Pages/Catalog/Catalog.razor";
        const string TheSharedMapping =
            "src/TrainingHub.Shared.Api/Contracts/Mappings/CatalogSearchMappings.cs";

        static string Text(string relative) => SourceTree.ReadText(Path.Combine(
            SourceTree.RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

        new[]
        {
            (File: TheCatalogPage, Text: Text(TheClientsPages + TheCatalogPage),
                Shape: "@page \"/\""),
            (File: TheCatalogPage, Text: Text(TheClientsPages + TheCatalogPage),
                Shape: "@page \"/catalog\""),
            (File: TheDecidingFile, Text: Text(TheDecidingFile),
                Shape: "string.Equals(path, \"/\", StringComparison.Ordinal)"),
            (File: TheSharedMapping, Text: Text(TheSharedMapping),
                Shape: ": CatalogOrder.Newest")
        }
            .Selected("shape the front door must keep")
            .Where(claim => !claim.Text.Contains(claim.Shape, StringComparison.Ordinal))
            .Select(claim =>
                $"'{claim.File}' no longer contains '{claim.Shape}'. The catalog is the front " +
                "door: one page behind two addresses, prerendered at the root, newest first by " +
                "default (ADR 0074)")
            .ShouldHold();
    }
}
