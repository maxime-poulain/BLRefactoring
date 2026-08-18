using Moq;
using TrainingHub.Shared.Application.Accounts;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behavior covered for the consumer that mints and mails a verification link (ADR 0090).
/// </summary>
/// <remarks>
/// The reset consumer's two pins — one invitation becomes exactly one email, a null mint becomes
/// nothing at all — plus the one this consumer adds: the words are the composer's, asked for in
/// the fact's culture, and this layer forwards them without reading them.
/// </remarks>
public sealed class SendVerificationLinkWhenEmailVerificationRequestedIntegrationEventHandlerTests
{
    private static readonly Guid KnownUserId = Guid.NewGuid();

    private readonly Mock<IEmailVerificationTokenStore> _verificationTokens = new();
    private readonly Mock<IVerificationEmailComposer> _composer = new();
    private readonly Mock<IEmailSender> _emailSender = new();

    /// <summary>
    /// Handle, an account that is gone or already verified, sends nothing.
    /// </summary>
    [Fact]
    public async Task Handle_AnAccountThatIsGoneOrAlreadyVerified_SendsNothing()
    {
        // Arrange
        _verificationTokens
            .Setup(store => store.IssueAsync(KnownUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailVerificationInvitation?)null);

        var sut = new SendVerificationLinkWhenEmailVerificationRequestedIntegrationEventHandler(
            _verificationTokens.Object, _composer.Object, _emailSender.Object);

        // Act
        await sut.HandleAsync(
            new EmailVerificationRequestedIntegrationEvent(KnownUserId, "en"), CancellationToken.None);

        // Assert
        _emailSender.Verify(
            sender => sender.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _composer.Verify(
            composer => composer.Compose(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// Handle, a minted invitation, mails the composers words to the accounts address.
    /// </summary>
    [Fact]
    public async Task Handle_AMintedInvitation_MailsTheComposersWordsToTheAccountsAddress()
    {
        // Arrange
        _verificationTokens
            .Setup(store => store.IssueAsync(KnownUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailVerificationInvitation(
                "https://web.test/verify-email?token=the-minted-token",
                "grace.hopper",
                "grace.hopper@example.org"));
        _composer
            .Setup(composer => composer.Compose(
                "fr", "grace.hopper", "https://web.test/verify-email?token=the-minted-token"))
            .Returns(new VerificationEmail("the composed subject", "the composed body"));

        var sut = new SendVerificationLinkWhenEmailVerificationRequestedIntegrationEventHandler(
            _verificationTokens.Object, _composer.Object, _emailSender.Object);

        // Act
        await sut.HandleAsync(
            new EmailVerificationRequestedIntegrationEvent(KnownUserId, "fr"), CancellationToken.None);

        // Assert
        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == "grace.hopper@example.org"
                    && message.Subject == "the composed subject"
                    && message.Body == "the composed body"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
