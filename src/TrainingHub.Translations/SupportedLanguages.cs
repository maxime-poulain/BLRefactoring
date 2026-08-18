namespace TrainingHub.Translations;

/// <summary>
/// The languages this application answers in, and the one it falls back to.
/// </summary>
/// <remarks>
/// One authority for every place that needs the list — the hosts' request-localization options,
/// the BFF's culture endpoint, the WebAssembly boot and the language selector — because two lists
/// would drift, and a culture one leg offers while another refuses it is exactly the disagreement
/// ADR 0088 exists to prevent. Neutral codes rather than regional ones: a browser asking for
/// <c>fr-BE</c> falls back to <c>fr</c> through the framework's own parent chain, and a regional
/// variant is added the day its prose diverges, not before.
/// </remarks>
public static class SupportedLanguages
{
    /// <summary>The fallback, and the language of the repository itself.</summary>
    public const string Default = "en";

    /// <summary>Every language the application answers in, the default included.</summary>
    public static readonly IReadOnlyList<string> All = ["en", "fr", "ru"];
}
