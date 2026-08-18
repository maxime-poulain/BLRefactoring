using System.Globalization;
using Microsoft.Extensions.Localization;
using TrainingHub.Shared.Application.Notifications;
using TrainingHub.Translations;

namespace TrainingHub.Shared.Infrastructure.Email;

/// <summary>
/// Composes the verification email from <see cref="NotificationResources"/>, in the culture the
/// requester was being served in.
/// </summary>
/// <remarks>
/// The presenter half of the email mechanics whose gateway half is <see cref="SmtpEmailSender"/>:
/// the application hands over facts — a culture code, a name, a link — and receives finished
/// prose, the same inversion the token store performs for the link (ADR 0090). This namespace is
/// the one place in the infrastructure allowed to name <c>TrainingHub.Translations</c>, and a
/// rule fences it here: prose for humans is composed beside the adapter that delivers it, never
/// in a repository or an interceptor, because a persisted fact does not vary with a language
/// (ADR 0088). The localizer reads <see cref="CultureInfo.CurrentUICulture"/> and nothing else,
/// so the culture is pinned around the reads and restored whatever happens — the outbox worker's
/// thread serves every consumer in turn and must not keep a language a previous message chose.
/// </remarks>
public sealed class VerificationEmailComposer(IStringLocalizer<NotificationResources> localizer)
    : IVerificationEmailComposer
{
    /// <inheritdoc />
    public VerificationEmail Compose(string culture, string username, string link)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(link);

        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = Resolve(culture);

        try
        {
            var body = localizer["VerifyEmailGreeting", username]
                + "\n\n"
                + localizer["VerifyEmailIntro"]
                + "\n\n"
                + link
                + "\n\n"
                + localizer["VerifyEmailLatestOnly"]
                + "\n\n"
                + localizer["VerifyEmailIgnore"];

            return new VerificationEmail(localizer["VerifyEmailSubject"], body);
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    /// <summary>
    /// The culture to compose in, or the default when the code names none.
    /// </summary>
    /// <remarks>
    /// The event's culture is written by the endpoint from the request's resolved culture, so a
    /// junk code should never arrive — but an outbox row is retried for days, and a message that
    /// would rather arrive in English than poison the delivery falls back instead of throwing.
    /// </remarks>
    private static CultureInfo Resolve(string culture)
    {
        try
        {
            return CultureInfo.GetCultureInfo(culture);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(SupportedLanguages.Default);
        }
    }
}
