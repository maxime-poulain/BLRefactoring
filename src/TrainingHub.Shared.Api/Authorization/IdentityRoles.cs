namespace TrainingHub.Shared.Api.Authorization;

/// <summary>
/// The roles this product grants, as ASP.NET Identity stores them.
/// </summary>
/// <remarks>
/// Distinct from <see cref="AdministratorPolicy"/> even though the two strings coincide today: a
/// role is a fact about an account, a policy is a rule about a request, and only the second is
/// named by an endpoint. Keeping them apart is what lets a policy grow a second condition — or a
/// second role satisfy it — without every <c>[Authorize]</c> in the solution changing.
/// <para>
/// Named here and in exactly two other places: the policy that requires it and the seeder that
/// grants it. No inner layer names a role at all, which is a rule (ADR 0051).
/// </para>
/// </remarks>
public static class IdentityRoles
{
    /// <summary>
    /// The authority over the catalogue: suspends a trainer, reinstates them, and acts on a
    /// training its owner may not.
    /// </summary>
    /// <remarks>
    /// Granted by a start-up seeder in Development and by a documented database operation
    /// everywhere else. There is deliberately no endpoint that grants a role.
    /// </remarks>
    public const string Administrator = "Administrator";
}
