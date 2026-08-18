namespace TrainingHub.Translations;

/// <summary>
/// Marker for the account surface's words — signing in, registering, recovering a password, and
/// erasing the account.
/// </summary>
/// <remarks>
/// Never instantiated and holding nothing: <c>IStringLocalizer&lt;AuthenticationResources&gt;</c>
/// uses the type to find the resource files sitting beside it, one per language. See ADR 0089.
/// </remarks>
public sealed class AuthenticationResources;
