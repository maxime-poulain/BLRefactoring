using BLRefactoring.FastEndpoints.Application.Features.Trainers.Create.Mailing;
using BLRefactoring.FastEndpoints.Domain;
using MediatR;

namespace BLRefactoring.FastEndpoints.Application.Features.Trainers.Create.Handlers;

public class SendMailWhenTrainerCreatedEventHandler : INotificationHandler<TrainerCreatedEvent>
{
    private readonly ITrainerCreationEmailService _mailingService;

    public SendMailWhenTrainerCreatedEventHandler(ITrainerCreationEmailService mailingService)
    {
        _mailingService = mailingService;
    }

    public Task Handle(TrainerCreatedEvent notification, CancellationToken cancellationToken)
    {
        // In a microservice architecture an Integration Event would be published here.
        return _mailingService.NotifyAsync(notification.TrainerId,
            cancellationToken);
    }
}
