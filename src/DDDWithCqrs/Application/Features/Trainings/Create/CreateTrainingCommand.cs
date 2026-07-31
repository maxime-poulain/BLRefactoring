using System.Text.Json.Serialization;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Application.Factories;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;

public class CreateTrainingCommand : ICommand<Result>
{
    [JsonIgnore] public Guid TrainingId { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = null!;
    public List<string> Topics { get; init; } = [];
    public string Description { get; init; } = null!;
    public string Prerequisites { get; init; } = null!;
    public string AcquiredSkills { get; init; } = null!;
}

public class CreateTrainingCommandHandler(
    ITrainingRepository trainingRepository,
    ITrainerRepository trainerRepository,
    IUniquenessTitleChecker titleChecker,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateTrainingCommand, Result>
{
    public async ValueTask<Result> Handle(
        CreateTrainingCommand request,
        CancellationToken cancellationToken)
    {
        var trainerId = TrainerId.Create(currentUserService.TrainerId);

        if (!await trainerRepository.ExistsAsync(trainerId, cancellationToken))
        {
            return Result.Failure(ErrorCode.NotFound,
                $"Trainer with id `{trainerId.Value}` not found.");
        }

        var detailsResult = TrainingDetailsFactory.Create(
            request.Title, request.Description, request.Prerequisites, request.AcquiredSkills, request.Topics);

        var trainingCreationResult = await detailsResult.MatchAsync(
            async details => await Training.CreateAsync(
                TrainingId.Create(request.TrainingId),
                trainerId,
                details.Title,
                details.Description,
                details.Prerequisites,
                details.AcquiredSkills,
                details.Topics,
                titleChecker,
                cancellationToken),
            Result<Training>.FailureAsync);

        return await trainingCreationResult.MatchAsync<Result>(async (training) =>
        {
            trainingRepository.Add(training);
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UniqueConstraintViolationException)
            {
                // A concurrent request slipped past the uniqueness pre-check;
                // the unique index is the authoritative guard, so a lost race
                // is the same business failure as a detected duplicate.
                return Result.Failure(ErrorCode.DuplicateTitle,
                    "A training with the same title already exists for this trainer.");
            }
            return Result.Success();
        }, Result.FailureAsync);
    }
}
