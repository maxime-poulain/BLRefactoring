using System.Globalization;
using Microsoft.Extensions.Localization;
using TrainingHub.Shared.Application.Notifications;
using TrainingHub.Translations;

namespace TrainingHub.Shared.Infrastructure.Email;

/// <summary>
/// Composes every notice this application sends from <see cref="NotificationResources"/>, in the
/// language its recipient reads.
/// </summary>
/// <remarks>
/// The presenter half of the email mechanics whose gateway half is <see cref="SmtpEmailSender"/>.
/// This namespace is the one place in the infrastructure allowed to name
/// <c>TrainingHub.Translations</c>, and a rule fences it here: prose for humans is composed beside
/// the adapter that delivers it, never in a repository or an interceptor, because a persisted fact
/// does not vary with a language (ADR 0088, ADR 0090).
/// <para>
/// The localizer reads <see cref="CultureInfo.CurrentUICulture"/> and nothing else, so the culture
/// is pinned around every composition and restored whatever happens — the outbox worker's thread
/// serves every consumer in turn and must not keep a language a previous message chose. That is
/// what <see cref="Compose"/> exists for, and it is why no method here reads the localizer
/// directly.
/// </para>
/// <para>
/// Every body is a list of paragraphs joined by a blank line. Each paragraph is one key, which is
/// what keeps a translation reviewable — a translator reads a sentence, not a page — and what lets
/// <c>EveryCultureResource_CarriesExactlyTheDefaultsKeys</c> report a missing one by name.
/// </para>
/// </remarks>
public sealed class NotificationComposer(IStringLocalizer<NotificationResources> localizer)
    : INotificationComposer
{
    /// <summary>What separates two paragraphs of a plain-text body.</summary>
    private const string Break = "\n\n";

    /// <inheritdoc />
    public Notification Welcome(string? language, string recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);

        return Compose(language, () => new Notification(
            localizer["WelcomeSubject"],
            Paragraphs(
                localizer["WelcomeGreeting", recipient],
                localizer["WelcomeBody"])));
    }

    /// <inheritdoc />
    public Notification VerificationLink(string? language, string username, string link)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(link);

        return Compose(language, () => new Notification(
            localizer["VerifyEmailSubject"],
            Paragraphs(
                localizer["VerifyEmailGreeting", username],
                localizer["VerifyEmailIntro"],
                link,
                localizer["VerifyEmailLatestOnly"],
                localizer["VerifyEmailIgnore"])));
    }

    /// <inheritdoc />
    public Notification ContactEmailChanged(string? language, string newContactEmail)
    {
        ArgumentNullException.ThrowIfNull(newContactEmail);

        return Compose(language, () => new Notification(
            localizer["ContactEmailChangedSubject"],
            Paragraphs(
                localizer["ContactEmailChangedBody", newContactEmail],
                localizer["ContactEmailChangedWarning"])));
    }

    /// <inheritdoc />
    public Notification AccountErased(string? language, string username)
    {
        ArgumentNullException.ThrowIfNull(username);

        return Compose(language, () => new Notification(
            localizer["AccountErasedSubject"],
            Paragraphs(
                localizer["AccountErasedGreeting", username],
                localizer["AccountErasedBody"],
                localizer["AccountErasedFarewell"],
                localizer["AccountErasedIntrusion"])));
    }

    /// <inheritdoc />
    public Notification PasswordChanged(string? language, string username)
    {
        ArgumentNullException.ThrowIfNull(username);

        return Compose(language, () => new Notification(
            localizer["PasswordChangedSubject"],
            Paragraphs(
                localizer["PasswordChangedGreeting", username],
                localizer["PasswordChangedBody"],
                localizer["PasswordChangedReassurance"],
                localizer["PasswordChangedIntrusion"])));
    }

    /// <inheritdoc />
    public Notification PasswordResetLink(string? language, string username, string link, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(link);

        // Whole minutes, computed before the culture is pinned and passed as a number: the store
        // stamps the expiry, so the email announces the window the database enforces (ADR 0084).
        var minutes = (int)lifetime.TotalMinutes;

        return Compose(language, () => new Notification(
            localizer["PasswordResetSubject"],
            Paragraphs(
                localizer["PasswordResetGreeting", username],
                localizer["PasswordResetIntro"],
                link,
                localizer["PasswordResetLifetime", minutes],
                localizer["PasswordResetIgnore"])));
    }

    /// <inheritdoc />
    public Notification Suspension(string? language, string recipient, string reason)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(reason);

        return Compose(language, () => new Notification(
            localizer["SuspensionSubject"],
            Paragraphs(
                localizer["SuspensionGreeting", recipient],
                localizer["SuspensionReason", reason],
                localizer["SuspensionConsequence"],
                localizer["SuspensionRecourse"])));
    }

    /// <inheritdoc />
    public Notification Reinstatement(string? language, string recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);

        return Compose(language, () => new Notification(
            localizer["ReinstatementSubject"],
            Paragraphs(
                localizer["ReinstatementGreeting", recipient],
                localizer["ReinstatementBody"])));
    }

    /// <inheritdoc />
    public Notification Withholding(string? language, string recipient, string title, string reason)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(reason);

        return Compose(language, () => new Notification(
            localizer["WithholdingSubject"],
            Paragraphs(
                localizer["WithholdingGreeting", recipient],
                localizer["WithholdingReason", title, reason],
                localizer["WithholdingConsequence"],
                localizer["WithholdingRecourse"])));
    }

    /// <inheritdoc />
    public Notification ContactMessage(
        string? language,
        string recipient,
        string sender,
        string senderEmailAddress,
        string message,
        string? about)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(senderEmailAddress);
        ArgumentNullException.ThrowIfNull(message);

        return Compose(language, () => new Notification(
            about is null
                ? localizer["ContactMessageSubject", sender]
                : localizer["ContactMessageSubjectAbout", sender, about],
            Paragraphs(
                localizer["ContactMessageGreeting", recipient],
                about is null
                    ? localizer["ContactMessageIntro", sender, senderEmailAddress]
                    : localizer["ContactMessageIntroAbout", sender, senderEmailAddress, about],
                message,
                localizer["ContactMessageReply"])));
    }

    /// <summary>Joins the paragraphs of a plain-text body.</summary>
    private static string Paragraphs(params string[] paragraphs) =>
        string.Join(Break, paragraphs);

    /// <summary>
    /// Runs a composition with the reader's culture pinned, and puts back whatever was there.
    /// </summary>
    private static Notification Compose(string? language, Func<Notification> compose)
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = Resolve(language);

        try
        {
            return compose();
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    /// <summary>
    /// The culture to compose in, or the default when the account named none this framework knows.
    /// </summary>
    /// <remarks>
    /// Two silences arrive here and both mean the same thing: an account that never chose a
    /// language, and a code the framework cannot parse. An outbox row is retried for days, and a
    /// message that would rather arrive in English than poison the delivery falls back instead of
    /// throwing. This is the one place in the application that names the default, which is why the
    /// read port passes its <see langword="null"/> through rather than filling it (ADR 0091).
    /// </remarks>
    private static CultureInfo Resolve(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return CultureInfo.GetCultureInfo(SupportedLanguages.Default);
        }

        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(SupportedLanguages.Default);
        }
    }
}
