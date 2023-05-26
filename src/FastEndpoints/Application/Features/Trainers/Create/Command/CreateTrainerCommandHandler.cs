using BLRefactoring.FastEndpoints.Application.ThirdParty.EfCore;
using BLRefactoring.FastEndpoints.Domain;
using FastEndpoints;
using MediatR;

namespace BLRefactoring.FastEndpoints.Application.Features.Trainers.Create.Command;

/// <summary>
/// Represents the handler for the <see cref="CreateTrainerCommand"/>.
/// </summary>
public class CreateTrainerCommandHandler : ICommandHandler<CreateTrainerCommand, TrainerDto>
{
    private readonly TrainingContext _trainingContext;
    private readonly IPublisher _publisher;

    public CreateTrainerCommandHandler(TrainingContext trainingContext, IPublisher publisher)
    {
        _trainingContext = trainingContext;
        _publisher = publisher;
    }

    public async Task<TrainerDto> ExecuteAsync(
        CreateTrainerCommand command,
        CancellationToken ct = default)
    {
        // `CreateTrainerRequestValidator` is implicitly called by FastEndpoints.
        // We don't need to call it explicitly.
        // However this behavior can be disabled.
        var trainerEntity = new Trainer()
        {
            Email = command.Email,
            firstname = command.Firstname,
            lastname = command.Lastname,
            Id = Guid.NewGuid()
        };

        await _trainingContext.Trainers.AddAsync(trainerEntity, ct);
        await _trainingContext.SaveChangesAsync(ct);

        // We could also register the event
        // and have them published during the `SaveChangesAsync` call.
        // We do this way in DDD projects.
        await _publisher.Publish(new TrainerCreatedEvent(trainerEntity.Id), ct);

        return new TrainerDto()
        {
            Id = trainerEntity.Id,
            Email = trainerEntity.Email,
            Firstname = trainerEntity.firstname,
            Lastname = trainerEntity.lastname
        };
    }
}
