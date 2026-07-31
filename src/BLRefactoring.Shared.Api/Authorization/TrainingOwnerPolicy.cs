namespace BLRefactoring.Shared.Api.Authorization;

/// <summary>
/// The authorization policy guarding the training write endpoints.
/// </summary>
public static class TrainingOwnerPolicy
{
    /// <summary>
    /// Name under which the policy is registered and referenced from
    /// <c>[Authorize(Policy = ...)]</c>.
    /// </summary>
    /// <remarks>
    /// A <see langword="const"/> rather than a literal repeated at every use site: an attribute
    /// argument must be a compile-time constant, so this is the only form that lets the policy be
    /// named once. A typo in one of the copies used to mean an endpoint guarded by a policy that
    /// does not exist — which ASP.NET Core reports at request time, not at startup.
    /// </remarks>
    public const string Name = "TrainingOwner";
}
