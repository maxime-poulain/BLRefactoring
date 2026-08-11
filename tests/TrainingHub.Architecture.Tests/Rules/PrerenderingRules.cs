using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// Which routes prerender, and who decides (ADR 0072).
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
}
