using Microsoft.AspNetCore.Authorization;

namespace TrainingHub.Shared.Api.Authorization;

/// <summary>
/// Requires that the caller's account have proven its email address.
/// </summary>
public sealed class VerifiedTrainerRequirement : IAuthorizationRequirement;
