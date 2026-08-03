using TrainingHub.Shared;
using TrainingHub.Shared.Domain;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Application.Factories;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.Create;

/// <summary>
/// Asks that a trainer be created.
/// </summary>
public sealed class CreateTrainerCommand : ICommand<Result>
{
    /// <summary>
    /// The trainer's identifier.
    /// </summary>
    public Guid TrainerId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The identity account the trainer is created for.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// The trainer's first name, as the caller sent it.
    /// </summary>
    public string Firstname { get; init; } = null!;

    /// <summary>
    /// The trainer's last name, as the caller sent it.
    /// </summary>
    public string Lastname { get; init; } = null!;

    /// <summary>
    /// The initial contact address of the trainer. At registration it is seeded
    /// from the account email; the trainer can make it diverge afterwards through
    /// their profile.
    /// </summary>
    public string ContactEmail { get; init; } = null!;
}

/// <summary>
/// Runs <see cref="CreateTrainerCommand"/>.
/// </summary>
public sealed class CreateTrainerCommandHandler(
    ITrainerRepository trainerRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateTrainerCommand, Result>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
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
