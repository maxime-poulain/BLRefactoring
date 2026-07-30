using System.Text.Json.Serialization;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.Messages;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;

public class CreateTrainerCommand : ICommand<Result>
{
    [JsonIgnore]
    public Guid TrainerId { get; init; } = Guid.NewGuid();
    [JsonIgnore]
    public Guid UserId { get; init; }
    public string Firstname { get; init; } = null!;
    public string Lastname { get; init; } = null!;
    public string Email { get; init; } = null!;
}

public class CreateTrainerCommandHandler(
    ITrainerRepository trainerRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateTrainerCommand, Result>
{
    public async ValueTask<Result> Handle(CreateTrainerCommand request, CancellationToken cancellationToken)
    {
        var message = new TrainerCreationMessage
        {
            TrainerId = TrainerId.Create(request.TrainerId),
            Firstname = request.Firstname,
            Lastname = request.Lastname,
            Email = request.Email,
            UserId = UserId.Create(request.UserId),
            Bio = "<>"
        };

        var trainerResult = Trainer.Create(message);

        return await trainerResult.MatchAsync(async trainer =>
        {
            trainerRepository.Add(trainer);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }, Result.FailureAsync);
    }
}
