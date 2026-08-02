using Microsoft.AspNetCore.Authorization;

namespace BLRefactoring.Shared.Api.Authorization;

/// <summary>
/// Requires that the caller own the training the route names.
/// </summary>
public sealed class TrainingOwnerRequirement : IAuthorizationRequirement;
