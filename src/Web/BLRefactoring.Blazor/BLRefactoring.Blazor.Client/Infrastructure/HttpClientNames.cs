namespace BLRefactoring.Blazor.Client.Infrastructure;

/// <summary>
/// The two clients this application uses, both pointed at its own origin.
/// </summary>
/// <remarks>
/// There is no configured API address any more. The browser talks to the host that served it, and
/// that host forwards to the API — so the front end cannot be pointed at the wrong backend, and it
/// no longer depends on the API's CORS policy to reach it at all. The API keeps that policy for
/// callers that are not this application. See ADR 0009.
/// </remarks>
public static class HttpClientNames
{
    /// <summary>Calls the API through the BFF's proxy, under <c>/api</c>.</summary>
    public const string Api = "Api";

    /// <summary>Calls the BFF's own endpoints, under <c>/bff</c>.</summary>
    public const string Bff = "Bff";
}
