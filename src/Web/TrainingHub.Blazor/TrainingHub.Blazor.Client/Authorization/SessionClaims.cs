namespace TrainingHub.Blazor.Client.Authorization;

/// <summary>
/// The claim names this browser reads out of the session cookie's identity.
/// </summary>
/// <remarks>
/// They are the API's own, minted by <c>TokenService</c> and carried into the cookie verbatim
/// because no inbound map shortens them. They are written again here rather than shared, because
/// this project references the generated clients and nothing else: a browser that took a project
/// reference on the API's assembly to read three strings would have inverted the dependency the
/// whole boundary exists to keep. What makes the copy safe is not discipline but a rule —
/// <c>TheBrowsersTrainerDoors_AskTheApisOwnQuestion</c> fails the build the moment either side
/// changes a name without the other (ADR 0078).
/// </remarks>
public static class SessionClaims
{
    /// <summary>
    /// The trainer the signed-in account is, absent from the identity of an account that is
    /// nobody's trainer — an administrator, for instance.
    /// </summary>
    /// <remarks>
    /// Absence is the whole signal, and it is the same question the API's own trainer policy asks.
    /// </remarks>
    public const string TrainerId = "trainer_id";

    /// <summary>The signed-in person's given name, absent when the account names no person.</summary>
    public const string Firstname = "firstname";

    /// <summary>The signed-in person's family name, absent when the account names no person.</summary>
    public const string Lastname = "lastname";
}
