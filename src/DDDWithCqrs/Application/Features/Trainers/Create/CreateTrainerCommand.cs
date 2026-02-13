using System.Text.Json.Serialization;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.Messages;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;

public class CreateTrainerCommand : ICommand<Result>
{
    [JsonIgnore]
    public Guid TrainerId { get; init; } = Guid.NewGuid();
    public string Firstname { get; init; } = null!;
    public string Lastname { get; init; } = null!;
    public string Email { get; init; } = null!;
}

public class CreateTrainerCommandHandler(ITrainerRepository trainerRepository)
    : ICommandHandler<CreateTrainerCommand, Result>
{
    public async ValueTask<Result> Handle(CreateTrainerCommand request, CancellationToken cancellationToken)
    {
        var message = new TrainerCreationMessage
        {
            Firstname = request.Firstname,
            Lastname = request.Lastname,
            Email = request.Email,
            UserId = Guid.Empty,
            Bio = "<>"
        };

        var trainerResult = Trainer.Create(message);

        return await trainerResult.MatchAsync(async trainer =>
        {
            await trainerRepository.SaveAsync(trainer, cancellationToken);
            return Result.Success();
        }, Result.FailureAsync);
    }
}
