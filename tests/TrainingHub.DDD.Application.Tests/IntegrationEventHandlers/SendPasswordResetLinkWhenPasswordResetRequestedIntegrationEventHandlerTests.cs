using Moq;
using TrainingHub.Shared.Application.Accounts;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behavior covered for the consumer that mints and mails a reset link (ADR 0084).
/// </summary>
/// <remarks>
/// The two answers the store can give are the two behaviors worth pinning: an invitation becomes
/// exactly one email to exactly the asked address, and an unknown address becomes nothing at all —
/// no email, and nothing else observable, because silence is the anti-enumeration posture.
/// </remarks>
public sealed class SendPasswordResetLinkWhenPasswordResetRequestedIntegrationEventHandlerTests
{
    private const string AskedAddress = "grace.hopper@example.org";

    private readonly Mock<IPasswordResetTokenStore> _resetTokens = new();
    private readonly Mock<INotificationComposer> _composer = new();
    private readonly Mock<IEmailSender> _emailSender = new();

    /// <summary>
    /// Handle, an address no account listens at, sends nothing.
    /// </summary>
    [Fact]
    public async Task Handle_AnAddressNoAccountListensAt_SendsNothing()
    {
        // Arrange
        _resetTokens
            .Setup(store => store.IssueAsync(AskedAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordResetInvitation?)null);

        var sut = new SendPasswordResetLinkWhenPasswordResetRequestedIntegrationEventHandler(
            _resetTokens.Object, _composer.Object, _emailSender.Object);

        // Act
        await sut.HandleAsync(new PasswordResetRequestedIntegrationEvent(AskedAddress), CancellationToken.None);

        // Assert
        _emailSender.Verify(
            sender => sender.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Handle, a known address, mails the composer's words to the account's own address.
    /// </summary>
    /// <remarks>
    /// The recipient is the invitation's address and not the one the visitor typed, and the
    /// language is the invitation's too: the mint read the account, so it answers both facts about
    /// its owner at once (ADR 0091).
    /// </remarks>
    [Fact]
    public async Task Handle_AKnownAddress_MailsTheComposersWordsToTheAccountsOwnAddress()
    {
        // Arrange
        _resetTokens
            .Setup(store => store.IssueAsync(AskedAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordResetInvitation(
                "https://web.test/reset-password?token=the-minted-token",
                "grace.hopper",
                TimeSpan.FromMinutes(15),
                AskedAddress,
                "fr"));
        _composer
            .Setup(composer => composer.PasswordResetLink(
                "fr",
                "grace.hopper",
                "https://web.test/reset-password?token=the-minted-token",
                TimeSpan.FromMinutes(15)))
            .Returns(new Notification("the composed subject", "the composed body"));

        var sut = new SendPasswordResetLinkWhenPasswordResetRequestedIntegrationEventHandler(
            _resetTokens.Object, _composer.Object, _emailSender.Object);

        // Act
        await sut.HandleAsync(new PasswordResetRequestedIntegrationEvent(AskedAddress), CancellationToken.None);

        // Assert
        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == AskedAddress
                    && message.Subject == "the composed subject"
                    && message.Body == "the composed body"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
