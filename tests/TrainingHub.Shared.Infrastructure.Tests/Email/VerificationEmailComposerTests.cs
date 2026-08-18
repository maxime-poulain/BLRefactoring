using System.Globalization;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using TrainingHub.Shared.Infrastructure.Email;
using TrainingHub.Translations;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Email;

/// <summary>
/// Behavior covered for the presenter that turns a culture, a name and a link into a verification
/// email (ADR 0090).
/// </summary>
/// <remarks>
/// Through the real localizer over the real resource files, because the property under test is
/// the wiring itself: that the family resolves, that the culture parameter — not the test
/// runner's — picks the language, and that the worker's thread comes back speaking whatever it
/// spoke before. A substituted localizer would prove string concatenation and nothing else.
/// </remarks>
public sealed class VerificationEmailComposerTests
{
    private const string Username = "grace.hopper";
    private const string Link = "https://web.test/verify-email?token=the-minted-token";

    private readonly VerificationEmailComposer _sut = new(
        new ServiceCollection()
            .AddLogging()
            .AddLocalization()
            .BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<NotificationResources>>());

    /// <summary>
    /// Compose, the default culture, writes the english subject and body around the link.
    /// </summary>
    [Fact]
    public void Compose_TheDefaultCulture_WritesTheEnglishSubjectAndBodyAroundTheLink()
    {
        var email = _sut.Compose("en", Username, Link);

        email.Subject.Should().Be(
            "Verify your TrainingHub email address",
            "the subject is stable per language — it is the key the mailbox assertions find a message by");
        email.Body.Should().Contain($"Hello {Username},");
        email.Body.Should().Contain(Link);
        email.Body.Should().Contain(
            "Only the most recent link works",
            "the one behavior the reader must know: asking again kills this link");
    }

    /// <summary>
    /// Compose, the requested culture, answers that language whatever the thread speaks.
    /// </summary>
    [Fact]
    public void Compose_TheRequestedCulture_AnswersThatLanguageWhateverTheThreadSpeaks()
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru");

        try
        {
            var email = _sut.Compose("fr", Username, Link);

            email.Subject.Should().Be(
                "Vérifiez votre adresse e-mail TrainingHub",
                "the fact's culture decides the language — the worker's thread was speaking Russian");
            email.Body.Should().Contain($"Bonjour {Username},");
            email.Body.Should().Contain(Link);

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
    /// Compose, a culture that names nothing, falls back to the default instead of throwing.
    /// </summary>
    [Fact]
    public void Compose_ACultureThatNamesNothing_FallsBackToTheDefaultInsteadOfThrowing()
    {
        var email = _sut.Compose("not a culture", Username, Link);

        email.Subject.Should().Be(
            "Verify your TrainingHub email address",
            "an outbox row is retried for days, and a message would rather arrive in English " +
            "than poison the delivery");
    }
}
