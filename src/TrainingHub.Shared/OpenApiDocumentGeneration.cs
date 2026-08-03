using System.Reflection;

namespace TrainingHub.Shared;

/// <summary>
/// Tells whether this process exists only to produce the OpenAPI document, rather than to serve
/// requests.
/// </summary>
/// <remarks>
/// Producing the document loads the application: the tooling runs the entry point, takes the host
/// once it is built, and stops before <c>app.Run()</c>. Every statement above that line therefore
/// executes for real — including anything that would touch a database.
/// <para>
/// Note which process this detects. NSwag never loads this host: it reads a document from disk.
/// The process that loads it is <c>dotnet-getdocument</c>, the tool
/// <c>Microsoft.Extensions.ApiDescription.Server</c> runs after the build — the name is taken from
/// a real run, not from documentation, because the first guess at it was wrong. Looking for "NSwag"
/// would find nothing at all.
/// </para>
/// <para>
/// Two independent signals, because neither is sufficient alone. The environment variable is
/// explicit and covers anything launched through <c>scripts/generate-clients.sh</c>. The entry
/// assembly covers the case nobody set it — a build started from an IDE, or by hand with
/// <c>-p:OpenApiGenerateDocuments=true</c> — and is the signal that does not depend on remembering.
/// </para>
/// </remarks>
public static class OpenApiDocumentGeneration
{
    /// <summary>
    /// Set by <c>scripts/generate-clients.sh</c> around the build that emits the document.
    /// </summary>
    public const string EnvironmentVariable = "OPENAPI_GENERATION";

    /// <summary>
    /// Marker in the entry assembly name of the tool that loads the application to read its
    /// document.
    /// </summary>
    /// <remarks>
    /// Matched anywhere in the name rather than at the start: the assembly is
    /// <c>dotnet-getdocument</c>, so a prefix test failed silently and left the environment
    /// variable as the only working signal. Observed in a build log rather than assumed.
    /// </remarks>
    private const string DocumentToolAssemblyMarker = "getdocument";

    /// <summary>
    /// <see langword="true"/> when the application was loaded to emit its OpenAPI document.
    /// </summary>
    /// <remarks>
    /// Only the first signal can be staged by a test — the second is the identity of the process
    /// the tests themselves run in, and that is precisely what a test cannot change. What a test
    /// can prove is the other direction: an ordinary process is not mistaken for a generating one.
    /// </remarks>
    public static bool IsInProgress()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
        {
            return true;
        }

        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;

        return entryAssemblyName is not null
            && entryAssemblyName.Contains(DocumentToolAssemblyMarker, StringComparison.OrdinalIgnoreCase);
    }
}
