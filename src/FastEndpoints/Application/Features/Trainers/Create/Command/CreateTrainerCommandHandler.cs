using BLRefactoring.FastEndpoints.Application.ThirdParty.EfCore;
using FastEndpoints;

namespace BLRefactoring.FastEndpoints.Application.Features.Trainers.Create.Command;

/// <summary>
/// Represents the handler for the <see cref="CreateTrainerCommand"/>.
/// </summary>
public class CreateTrainerCommandHandler : ICommandHandler<CreateTrainerCommand, TrainerDto>
{
    private readonly TrainingContext _trainingContext;

    public CreateTrainerCommandHandler(TrainingContext trainingContext)
        => _trainingContext = trainingContext;

    public async Task<TrainerDto> ExecuteAsync(CreateTrainerCommand command, CancellationToken cancellationToken = default)
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

        await _trainingContext.Trainers.AddAsync(trainerEntity, cancellationToken);
        await _trainingContext.SaveChangesAsync(cancellationToken);

        return new TrainerDto()
        {
            Id = trainerEntity.Id,
            Email = trainerEntity.Email,
            Firstname = trainerEntity.firstname,
            Lastname = trainerEntity.lastname
        };
    }
}
