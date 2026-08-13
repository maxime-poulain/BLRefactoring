namespace TrainingHub.Blazor.Client.Authorization;

/// <summary>
/// The roles this browser reads out of the session cookie's identity.
/// </summary>
/// <remarks>
/// The name was a literal in four places before this class existed — three pages and the user
/// menu — which is four chances to mistype a string whose failure is silent: a role that is never
/// matched looks exactly like a caller who does not hold it. Written once, and held equal to the
/// API's <c>IdentityRoles.Administrator</c> by the rule defending ADR 0078.
/// </remarks>
public static class SessionRoles
{
    /// <summary>The authority over the administration's surface (ADR 0051).</summary>
    public const string Administrator = "Administrator";
}
