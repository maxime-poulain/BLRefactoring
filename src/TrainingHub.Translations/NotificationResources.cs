namespace TrainingHub.Translations;

/// <summary>
/// Marker for the words the application emails — the notices composed for an inbox rather than
/// for a screen.
/// </summary>
/// <remarks>
/// Never instantiated and holding nothing: <c>IStringLocalizer&lt;NotificationResources&gt;</c>
/// uses the type to find the resource files sitting beside it, one per language, and that is its
/// whole job (ADR 0088). A family of its own rather than a corner of an existing one, because
/// its reader is different in kind: these strings leave through SMTP, composed by the email
/// adapter in the infrastructure ring. All ten notices are here since ADR 0091 lifted ADR 0090's
/// deferral, each body a run of paragraph keys the composer joins.
/// </remarks>
public sealed class NotificationResources;
