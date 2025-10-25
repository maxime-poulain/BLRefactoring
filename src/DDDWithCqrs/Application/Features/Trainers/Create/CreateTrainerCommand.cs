using System.Text.Json.Serialization;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;

public class CreateTrainerCommand : ICommand<Result>
{
    [JsonIgnore]
    public Guid TrainerId { get; init; } = Guid.NewGuid();
    public string Firstname { get; init; } = null!;
    public string Lastname { get; init; } = null!;
    public string Email { get; init; } = null!;
}

public class CreateTrainerCommandHandler : ICommandHandler<CreateTrainerCommand, Result>
{
    private readonly ITrainerRepository _trainerRepository;

    public CreateTrainerCommandHandler(ITrainerRepository trainerRepository)
    {
        _trainerRepository = trainerRepository;
    }

    public async ValueTask<Result> Handle(CreateTrainerCommand request, CancellationToken cancellationToken)
    {
        var trainerResult = Trainer.Create(request.TrainerId, request.Firstname, request.Lastname, request.Email);

        return await trainerResult.MatchAsync(async trainer =>
        {
            await _trainerRepository.SaveAsync(trainer, cancellationToken);
            return Result.Success();
        }, Result.FailureAsync);
    }
}
