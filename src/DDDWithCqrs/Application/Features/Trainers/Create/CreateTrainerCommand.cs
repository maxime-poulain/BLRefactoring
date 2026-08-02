using BLRefactoring.Shared;
using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Application.Factories;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;

public sealed class CreateTrainerCommand : ICommand<Result>
{
    public Guid TrainerId { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public string Firstname { get; init; } = null!;
    public string Lastname { get; init; } = null!;

    /// <summary>
    /// The initial contact address of the trainer. At registration it is seeded
    /// from the account email; the trainer can make it diverge afterwards through
    /// their profile.
    /// </summary>
    public string ContactEmail { get; init; } = null!;
}

public sealed class CreateTrainerCommandHandler(
    ITrainerRepository trainerRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateTrainerCommand, Result>
{
    public async ValueTask<Result> Handle(CreateTrainerCommand request, CancellationToken cancellationToken)
    {
        var profileResult = TrainerProfileFactory.Create(
            request.Firstname, request.Lastname, request.ContactEmail, bio: null);

        return await profileResult.MatchAsync(async profile =>
        {
            var trainer = Trainer.Create(
                TrainerId.Create(request.TrainerId),
                UserId.Create(request.UserId),
                profile.Name,
                profile.ContactEmail,
                profile.Bio);

            trainerRepository.Add(trainer);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }, Result.FailureAsync);
    }
}
