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

    public async Task<Result> Handle(CreateTrainerCommand request, CancellationToken cancellationToken)
    {
        var trainerResult = Trainer.Create(request.TrainerId, request.Firstname, request.Lastname, request.Email);

        if (trainerResult.IsFailure)
        {
            return Result.Failure(trainerResult.Errors);
        }

        await _trainerRepository.SaveAsync(trainerResult.Value, cancellationToken);
        return Result.Success();
    }
}
