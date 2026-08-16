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
            _resetTokens.Object, _emailSender.Object);

        // Act
        await sut.HandleAsync(new PasswordResetRequestedIntegrationEvent(AskedAddress), CancellationToken.None);

        // Assert
        _emailSender.Verify(
            sender => sender.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Handle, a known address, mails the invitation's link and window to the asked address.
    /// </summary>
    [Fact]
    public async Task Handle_AKnownAddress_MailsTheInvitationsLinkAndWindow()
    {
        // Arrange
        _resetTokens
            .Setup(store => store.IssueAsync(AskedAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordResetInvitation(
                "https://web.test/reset-password?token=the-minted-token",
                "grace.hopper",
                TimeSpan.FromMinutes(15)));

        var sut = new SendPasswordResetLinkWhenPasswordResetRequestedIntegrationEventHandler(
            _resetTokens.Object, _emailSender.Object);

        // Act
        await sut.HandleAsync(new PasswordResetRequestedIntegrationEvent(AskedAddress), CancellationToken.None);

        // Assert
        _emailSender.Verify(sender => sender.SendAsync(
                It.Is<EmailMessage>(message =>
                    message.Recipient == AskedAddress
                    && message.Subject == "Reset your TrainingHub password"
                    && message.Body.Contains("grace.hopper")
                    && message.Body.Contains("https://web.test/reset-password?token=the-minted-token")
                    && message.Body.Contains("15 minutes")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
