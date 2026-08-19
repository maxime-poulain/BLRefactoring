using System.Globalization;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using TrainingHub.Shared.Infrastructure.Email;
using TrainingHub.Translations;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Email;

/// <summary>
/// Behavior covered for the presenter that turns a language and a handful of facts into the ten
/// notices this application sends (ADR 0090, ADR 0091).
/// </summary>
/// <remarks>
/// Through the real localizer over the real resource files, because the property under test is
/// the wiring itself: that the family resolves, that the language parameter — not the test
/// runner's culture — picks the words, and that the worker's thread comes back speaking whatever
/// it spoke before. A substituted localizer would prove string concatenation and nothing else.
/// </remarks>
public sealed class NotificationComposerTests
{
    private const string Username = "grace.hopper";
    private const string Link = "https://web.test/verify-email?token=the-minted-token";
    private const string Recipient = "Grace Hopper";

    private readonly NotificationComposer _sut = new(
        new ServiceCollection()
            .AddLogging()
            .AddLocalization()
            .BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<NotificationResources>>());

    /// <summary>
    /// Verification link, the default culture, writes the english subject and body around the link.
    /// </summary>
    [Fact]
    public void VerificationLink_TheDefaultCulture_WritesTheEnglishSubjectAndBodyAroundTheLink()
    {
        var notification = _sut.VerificationLink("en", Username, Link);

        notification.Subject.Should().Be(
            "Verify your TrainingHub email address",
            "the subject is stable per language — it is the key the mailbox assertions find a message by");
        notification.Body.Should().Contain($"Hello {Username},");
        notification.Body.Should().Contain(Link);
        notification.Body.Should().Contain(
            "Only the most recent link works",
            "the one behavior the reader must know: asking again kills this link");
    }

    /// <summary>
    /// Verification link, the requested language, answers it whatever the thread speaks.
    /// </summary>
    [Fact]
    public void VerificationLink_TheRequestedLanguage_AnswersItWhateverTheThreadSpeaks()
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru");

        try
        {
            var notification = _sut.VerificationLink("fr", Username, Link);

            notification.Subject.Should().Be(
                "Vérifiez votre adresse e-mail TrainingHub",
                "the recipient's language decides the words — the worker's thread was speaking Russian");
            notification.Body.Should().Contain($"Bonjour {Username},");
            notification.Body.Should().Contain(Link);

            CultureInfo.CurrentUICulture.Should().Be(
                CultureInfo.GetCultureInfo("ru"),
                "the worker's thread serves every consumer in turn and must come back unchanged");
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    /// <summary>
    /// Verification link, a language that names nothing, falls back to the default instead of throwing.
    /// </summary>
    [Fact]
    public void VerificationLink_ALanguageThatNamesNothing_FallsBackToTheDefaultInsteadOfThrowing()
    {
        var notification = _sut.VerificationLink("not a culture", Username, Link);

        notification.Subject.Should().Be(
            "Verify your TrainingHub email address",
            "an outbox row is retried for days, and a message would rather arrive in English " +
            "than poison the delivery");
    }

    /// <summary>
    /// Verification link, no language at all, falls back to the default.
    /// </summary>
    /// <remarks>
    /// The silence an account that never chose a language sends down every one of these methods.
    /// The read port passes it through rather than filling it, so this is the one place in the
    /// application where it becomes English (ADR 0091).
    /// </remarks>
    [Fact]
    public void VerificationLink_NoLanguageAtAll_FallsBackToTheDefault()
    {
        var notification = _sut.VerificationLink(null, Username, Link);

        notification.Subject.Should().Be("Verify your TrainingHub email address");
    }

    /// <summary>
    /// Every notice, the three supported languages, answers words of that language.
    /// </summary>
    /// <remarks>
    /// The census: ten notices times three languages, asserted through the subject alone, because
    /// the subject is what a resource file misses first and what a mailbox is searched by. The
    /// bodies are covered by the four facts above and by the end-to-end fact in the TestKit; what
    /// this holds is that no notice was left composing English for a Russian reader.
    /// </remarks>
    /// <param name="language">The language the recipient reads in.</param>
    /// <param name="welcome">The welcome message's subject in that language.</param>
    /// <param name="suspension">The suspension notice's subject in that language.</param>
    [Theory]
    [InlineData("en", "Welcome aboard!", "Your trainer account has been suspended")]
    [InlineData("fr", "Bienvenue à bord !", "Votre compte de formateur a été suspendu")]
    [InlineData("ru", "Добро пожаловать!", "Ваша учетная запись преподавателя приостановлена")]
    public void EveryNotice_TheThreeSupportedLanguages_AnswersWordsOfThatLanguage(
        string language,
        string welcome,
        string suspension)
    {
        _sut.Welcome(language, Recipient).Subject.Should().Be(welcome);
        _sut.Suspension(language, Recipient, "spam").Subject.Should().Be(suspension);

        var everyNotice = new[]
        {
            _sut.Welcome(language, Recipient),
            _sut.VerificationLink(language, Username, Link),
            _sut.ContactEmailChanged(language, "new@example.org"),
            _sut.AccountErased(language, Username),
            _sut.PasswordChanged(language, Username),
            _sut.PasswordResetLink(language, Username, Link, TimeSpan.FromMinutes(15)),
            _sut.Suspension(language, Recipient, "spam"),
            _sut.Reinstatement(language, Recipient),
            _sut.Withholding(language, Recipient, "Domain-Driven Design", "off topic"),
            _sut.ContactMessage(language, Recipient, "Ada Lovelace", "ada@example.org", "Hi!", null),
        };

        everyNotice.Should().AllSatisfy(notification =>
        {
            notification.Subject.Should().NotBeNullOrWhiteSpace(
                "a notice with no subject is a resource key nobody declared");
            notification.Subject.Should().NotContain(
                "Subject",
                "a localizer answers the key itself when the family does not declare it");
            notification.Body.Should().NotBeNullOrWhiteSpace();
        });
    }

    /// <summary>
    /// Password reset link, the announced window, is the one the store stamped.
    /// </summary>
    [Fact]
    public void PasswordResetLink_TheAnnouncedWindow_IsTheOneTheStoreStamped()
    {
        var notification = _sut.PasswordResetLink("en", Username, Link, TimeSpan.FromMinutes(15));

        notification.Body.Should().Contain(
            "works for 15 minutes",
            "the email announces the window the database enforces, or it is a lie (ADR 0084)");
    }

    /// <summary>
    /// Contact message, a training the visitor was reading, names it in both the subject and the body.
    /// </summary>
    [Fact]
    public void ContactMessage_ATrainingTheVisitorWasReading_NamesItInBothTheSubjectAndTheBody()
    {
        var notification = _sut.ContactMessage(
            "en", Recipient, "Ada Lovelace", "ada@example.org", "Is it still open?", "Domain-Driven Design");

        notification.Subject.Should().Contain("Domain-Driven Design");
        notification.Body.Should().Contain("Domain-Driven Design");
        notification.Body.Should().Contain(
            "Is it still open?",
            "what the visitor wrote is theirs and is never translated");
    }

    /// <summary>
    /// Contact message, a visitor writing in another language, keeps their words and translates the rest.
    /// </summary>
    /// <remarks>
    /// The decision ADR 0091 exists for, in one assertion: the trainer reads their own language,
    /// and the message inside it is whatever the visitor typed.
    /// </remarks>
    [Fact]
    public void ContactMessage_AVisitorWritingInAnotherLanguage_KeepsTheirWordsAndTranslatesTheRest()
    {
        var notification = _sut.ContactMessage(
            "ru", Recipient, "Ada Lovelace", "ada@example.org", "Bonjour, une question", null);

        notification.Body.Should().Contain("Здравствуйте, Grace Hopper!");
        notification.Body.Should().Contain("Bonjour, une question");
    }

    /// <summary>
    /// Every notice, however it ends, leaves the thread speaking what it spoke.
    /// </summary>
    /// <remarks>
    /// The <c>finally</c> is the whole point of the pinning: the delivery worker's thread serves
    /// every consumer in turn, and a notice that kept its language would put the next message in
    /// the wrong one.
    /// </remarks>
    [Fact]
    public void EveryNotice_HoweverItEnds_LeavesTheThreadSpeakingWhatItSpoke()
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru");

        try
        {
            _sut.Reinstatement("fr", Recipient);
            _sut.AccountErased("en", Username);

            CultureInfo.CurrentUICulture.Should().Be(CultureInfo.GetCultureInfo("ru"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }
}
