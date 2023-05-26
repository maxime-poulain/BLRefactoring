using MediatR;

namespace BLRefactoring.FastEndpoints.Domain;

public class TrainerCreatedEvent : INotification
{
    public TrainerCreatedEvent(Guid trainerId)
    {
        TrainerId = trainerId;
    }

    public Guid TrainerId { get; init; }
}
