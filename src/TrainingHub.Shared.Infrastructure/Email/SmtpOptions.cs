namespace TrainingHub.Shared.Infrastructure.Email;

/// <summary>
/// Where the mail server is, who the messages come from, and how to authenticate.
/// </summary>
/// <remarks>
/// Everything that ties this solution to one mail server is in this class and nowhere else.
/// Pointing at a hosted relay instead of the local Mailpit container means changing
/// <see cref="Host"/>, the credentials and <see cref="UseStartTls"/> — no code, which is the claim
/// ADR 0031 makes and the reason the adapter speaks SMTP rather than any provider's own dialect.
/// </remarks>
public sealed class SmtpOptions
{
    /// <summary>
    /// The configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "Smtp";

    /// <summary>
    /// The host name of the mail server.
    /// </summary>
    public string Host { get; set; } = null!;

    /// <summary>
    /// The port the mail server listens on.
    /// </summary>
    /// <remarks>
    /// No default on purpose: 1025 for the local Mailpit container and 587 for a hosted relay are
    /// both common, and a value that silently fits one environment misconfigures the other.
    /// </remarks>
    public int Port { get; set; }

    /// <summary>
    /// The address the messages are sent from.
    /// </summary>
    /// <remarks>
    /// Deliberately here rather than on <c>EmailMessage</c>: which identity a deployment sends as
    /// is infrastructure configuration, like which bucket it writes to — the application composes
    /// recipient, subject and body, and that is the whole of its vocabulary.
    /// </remarks>
    public string SenderAddress { get; set; } = null!;

    /// <summary>
    /// The display name shown beside <see cref="SenderAddress"/>, when there is one.
    /// </summary>
    public string? SenderName { get; set; }

    /// <summary>
    /// The user name to authenticate with, when the server requires authentication.
    /// </summary>
    /// <remarks>
    /// Optional because the local container accepts anonymous submission; a hosted relay does not.
    /// Credentials travel as a pair — a start-up validation refuses one without the other.
    /// </remarks>
    public string? Username { get; set; }

    /// <summary>
    /// The password paired with <see cref="Username"/>.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Whether the connection is upgraded to TLS with STARTTLS before anything else is sent.
    /// </summary>
    /// <remarks>
    /// False for the local container, which speaks plain text; true for any relay reached across a
    /// network worth the name. A boolean rather than the client library's own enum keeps the
    /// options configuration-shaped — these two are the only modes this solution has a use for.
    /// </remarks>
    public bool UseStartTls { get; set; }
}
