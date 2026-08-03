namespace TrainingHub.Shared;

/// <summary>
/// Answers who is making the current request.
/// </summary>
/// <remarks>
/// A port, declared in the kernel and implemented against <c>HttpContext</c> in the API layer, so
/// that application code can ask whose request this is without depending on ASP.NET Core.
/// </remarks>
public interface ICurrentUserService
{
    /// <summary>
    /// The identity user behind the request — who signed in.
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// The trainer the signed-in user is, which is the identity the domain works in.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="UserId"/> on purpose: authentication knows about accounts, the
    /// domain knows about trainers, and conflating them is what lets one caller edit another's work.
    /// </remarks>
    Guid TrainerId { get; }
}
