using BLRefactoring.Shared.Application.IntegrationEvents;
using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// The side effects that leave this system, run after the transaction has committed.
/// </summary>
/// <remarks>
/// Same work as before, moved past the commit. Each of these must tolerate running twice on the
/// same message: the outbox delivers at least once, and a crash between a successful send and
/// the row being marked will replay it. A duplicate welcome email is a nuisance; a welcome email
/// for a trainer that does not exist — which is what the pre-commit version could produce — is a
/// defect.
/// </remarks>
public sealed class SendWelcomeEmailWhenTrainerRegisteredHandler(IEmailSender emailSender)
    : IIntegrationEventHandler<TrainerRegisteredIntegrationEvent>
{
    public Task HandleAsync(
        TrainerRegisteredIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage(
            integrationEvent.ContactEmail,
            "Welcome aboard!",
            $"Hello {integrationEvent.Firstname} {integrationEvent.Lastname}, " +
            "your trainer account has been created. You can now publish your first training.");

        return emailSender.SendAsync(message, cancellationToken);
    }
}

/// <inheritdoc cref="SendWelcomeEmailWhenTrainerRegisteredHandler"/>
public sealed class NotifyPreviousAddressWhenContactEmailChangedHandler(IEmailSender emailSender)
    : IIntegrationEventHandler<TrainerContactEmailChangedIntegrationEvent>
{
    public Task HandleAsync(
        TrainerContactEmailChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage(
            integrationEvent.PreviousContactEmail,
            "Your contact email address was changed",
            $"The contact email address of your trainer profile was changed to {integrationEvent.NewContactEmail}. " +
            "If you did not request this change, please contact support immediately.");

        return emailSender.SendAsync(message, cancellationToken);
    }
}

/// <inheritdoc cref="SendWelcomeEmailWhenTrainerRegisteredHandler"/>
public sealed class IndexTrainingWhenTrainingPublishedHandler(ITrainingSearchIndexer searchIndexer)
    : IIntegrationEventHandler<TrainingPublishedIntegrationEvent>
{
    public Task HandleAsync(
        TrainingPublishedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
        => searchIndexer.IndexAsync(
            integrationEvent.TrainingId.Value,
            integrationEvent.TrainerId.Value,
            cancellationToken);
}

/// <inheritdoc cref="SendWelcomeEmailWhenTrainerRegisteredHandler"/>
public sealed class IndexTrainingWhenTrainingRevisedHandler(ITrainingSearchIndexer searchIndexer)
    : IIntegrationEventHandler<TrainingRevisedIntegrationEvent>
{
    public Task HandleAsync(
        TrainingRevisedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
        => searchIndexer.IndexAsync(
            integrationEvent.TrainingId.Value,
            integrationEvent.TrainerId.Value,
            cancellationToken);
}
