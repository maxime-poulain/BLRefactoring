namespace TrainingHub.Blazor.Client.Infrastructure;

/// <summary>
/// Where the address of the signed-in trainer's own portrait comes from.
/// </summary>
/// <remarks>
/// The same read as the standing — one GET of the caller's own profile, shared per scope — asked
/// a second question: the photo's identity, which no claim carries because a portrait can change
/// mid-session while the token cannot (ADR 0057 makes the same argument for the standing). The
/// address answered is the authenticated route with the photo's identity as a cache buster, the
/// shape the profile page builds for itself (ADR 0063) — never the public portrait route, which
/// answers 404 for a suspended trainer who is still entitled to see their own face.
/// </remarks>
public interface ITrainerPortraitSource
{
    /// <summary>
    /// The portrait's address, or <c>null</c> when the caller has no photo — or is no trainer at
    /// all, an administrator being an account and not a person with a portrait.
    /// </summary>
    Task<string?> FindOwnPortraitAsync();
}
