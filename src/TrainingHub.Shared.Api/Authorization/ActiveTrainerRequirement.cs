using Microsoft.AspNetCore.Authorization;

namespace TrainingHub.Shared.Api.Authorization;

/// <summary>
/// Requires that the caller's standing be <c>Active</c>.
/// </summary>
public sealed class ActiveTrainerRequirement : IAuthorizationRequirement;
