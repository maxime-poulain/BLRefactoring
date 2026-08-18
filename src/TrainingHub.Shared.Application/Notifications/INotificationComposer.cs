namespace TrainingHub.Shared.Application.Notifications;

/// <summary>
/// The finished words of one notice: what a mail client shows in its list, and what it shows when
/// the message is opened.
/// </summary>
/// <param name="Subject">The subject line.</param>
/// <param name="Body">The plain-text body.</param>
public sealed record Notification(string Subject, string Body);

/// <summary>
/// Writes this application's outbound prose, in the language its reader asked for.
/// </summary>
/// <remarks>
/// The presenter half of the email mechanics whose gateway half is <see cref="IEmailSender"/>: a
/// consumer hands over facts — a language, a name, a link, a reason — and receives finished prose
/// it never inspects. The application layer knows no language, exactly as it knows no URL and no
/// SMTP host (ADR 0090, generalized to every notice by ADR 0091).
/// <para>
/// One port with ten methods rather than ten ports with one, and the ten are one concept: the
/// words this system says to a person. What keeps that from becoming a bag of unrelated methods
/// is the rule beside it — every consumer that sends an email composes through here, and none
/// writes a subject or a body of its own.
/// </para>
/// <para>
/// Every method takes the language first, and every caller passes the language it read where it
/// read the recipient's address. <see langword="null"/> is a legitimate answer — an account that
/// never chose — and means the same as a code no culture matches: the implementation falls back to
/// the application's default rather than refusing, because a message would rather arrive in
/// English than poison an outbox row for days.
/// </para>
/// </remarks>
public interface INotificationComposer
{
    /// <summary>Welcomes a trainer whose account has just been created.</summary>
    /// <param name="language">The language its reader reads in.</param>
    /// <param name="recipient">The trainer's name, as the greeting should say it.</param>
    Notification Welcome(string? language, string recipient);

    /// <summary>Invites an account to prove the address it registered with.</summary>
    /// <param name="language">The language its reader reads in.</param>
    /// <param name="username">The account's username, for the greeting.</param>
    /// <param name="link">The absolute address of the verification page.</param>
    Notification VerificationLink(string? language, string username, string link);

    /// <summary>Warns the address a trainer's profile just moved away from.</summary>
    /// <param name="language">The language its reader reads in.</param>
    /// <param name="newContactEmail">The address the profile now publishes.</param>
    Notification ContactEmailChanged(string? language, string newContactEmail);

    /// <summary>Confirms that an account, and everything it held, is gone.</summary>
    /// <param name="language">The language its reader reads in.</param>
    /// <param name="username">The username the account had, for the greeting.</param>
    Notification AccountErased(string? language, string username);

    /// <summary>Tells an account its password has just changed.</summary>
    /// <param name="language">The language its reader reads in.</param>
    /// <param name="username">The account's username, for the greeting.</param>
    Notification PasswordChanged(string? language, string username);

    /// <summary>Carries the link that lets a forgotten password be replaced.</summary>
    /// <param name="language">The language its reader reads in.</param>
    /// <param name="username">The account's username, for the greeting.</param>
    /// <param name="link">The absolute address of the reset page.</param>
    /// <param name="lifetime">How long the link stays valid, as the store stamped it.</param>
    Notification PasswordResetLink(string? language, string username, string link, TimeSpan lifetime);

    /// <summary>Tells a trainer they have been suspended, and why.</summary>
    /// <param name="language">The language its reader reads in.</param>
    /// <param name="recipient">The trainer's name, as the greeting should say it.</param>
    /// <param name="reason">The reason the administrator wrote.</param>
    Notification Suspension(string? language, string recipient, string reason);

    /// <summary>Tells a trainer their suspension has been lifted.</summary>
    /// <param name="language">The language its reader reads in.</param>
    /// <param name="recipient">The trainer's name, as the greeting should say it.</param>
    Notification Reinstatement(string? language, string recipient);

    /// <summary>Tells a trainer one of their trainings has been withheld, and why.</summary>
    /// <param name="language">The language its reader reads in.</param>
    /// <param name="recipient">The trainer's name, as the greeting should say it.</param>
    /// <param name="title">The title of the training that was withheld.</param>
    /// <param name="reason">The reason the administrator wrote.</param>
    Notification Withholding(string? language, string recipient, string title, string reason);

    /// <summary>Carries a visitor's message to the trainer they wrote to.</summary>
    /// <remarks>
    /// The one notice that forwards somebody else's words. <paramref name="message"/> is passed
    /// through untouched and never translated: it is the visitor's, in whatever language they
    /// chose to write it, and the words around it are the trainer's.
    /// </remarks>
    /// <param name="language">The language its reader reads in.</param>
    /// <param name="recipient">The trainer's name, as the greeting should say it.</param>
    /// <param name="sender">The visitor's name, as they gave it.</param>
    /// <param name="senderEmailAddress">The address an answer goes to.</param>
    /// <param name="message">What the visitor wrote.</param>
    /// <param name="about">
    /// The title of the training they were reading, or <see langword="null"/> when they wrote from
    /// the trainer's own page.
    /// </param>
    Notification ContactMessage(
        string? language,
        string recipient,
        string sender,
        string senderEmailAddress,
        string message,
        string? about);
}
