namespace TrainingHub.Shared.Application.Notifications;

/// <summary>
/// The finished words of a verification email: a subject line and a plain-text body, already in
/// the reader's language.
/// </summary>
/// <param name="Subject">The subject line, composed in the requested culture.</param>
/// <param name="Body">The plain-text body, the link inside it.</param>
public sealed record VerificationEmail(string Subject, string Body);

/// <summary>
/// Output boundary for the words of the verification email: the use case hands over the facts —
/// a culture, a name, a link — and receives finished prose it never inspects.
/// </summary>
/// <remarks>
/// Declared here so the consumer that sends the email can ask for its words without knowing where
/// they come from; the adapter lives in the infrastructure's email corner, beside the SMTP
/// gateway, because composing a message for a human is presenter's work and presenters belong to
/// the interface-adapters ring (ADR 0090). The same inversion
/// <see cref="Accounts.IPasswordResetTokenStore"/> already performs for the link — the
/// application receives a finished thing rather than the parts to build one — applied to
/// language: this layer stays clean of <c>TrainingHub.Translations</c> exactly as it stays clean
/// of URLs.
/// </remarks>
public interface IVerificationEmailComposer
{
    /// <summary>
    /// Composes the verification email in one culture.
    /// </summary>
    /// <param name="culture">The culture code the requester was being served in (ADR 0088).</param>
    /// <param name="username">The name the greeting addresses.</param>
    /// <param name="link">The finished verification link the body carries.</param>
    /// <returns>The subject and body, composed in that culture.</returns>
    VerificationEmail Compose(string culture, string username, string link);
}
