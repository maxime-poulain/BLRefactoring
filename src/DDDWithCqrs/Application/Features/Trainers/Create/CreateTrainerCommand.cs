using System.Text.Json.Serialization;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

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
        var trainerResult = Trainer.Create(request.Firstname, request.Lastname, request.Email, Guid.Empty);

        return await trainerResult.MatchAsync(async trainer =>
        {
            await trainerRepository.SaveAsync(trainer, cancellationToken);
            return Result.Success();
        }, Result.FailureAsync);
    }
}
