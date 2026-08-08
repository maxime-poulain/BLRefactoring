namespace TrainingHub.Shared.Api.Authorization;

/// <summary>
/// The authorization policy guarding the administrative endpoints.
/// </summary>
/// <remarks>
/// Carries no requirement of its own: unlike ownership, which is a question only the database can
/// answer, a role is already in the token, and the framework knows how to read one. The policy is
/// <c>RequireRole(IdentityRoles.Administrator)</c> and nothing more — see
/// <c>AuthorizationExtensions</c>.
/// <para>
/// A <see langword="const"/> for the same reason <see cref="TrainingOwnerPolicy.Name"/> is one: an
/// attribute argument must be a compile-time constant, and a policy named by a literal that does
/// not exist is reported at request time rather than at start-up.
/// </para>
/// </remarks>
public static class AdministratorPolicy
{
    /// <summary>
    /// Name under which the policy is registered and referenced from
    /// <c>[Authorize(Policy = ...)]</c>.
    /// </summary>
    public const string Name = "Administrator";
}
