using System.Text.Json.Serialization;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Application.Factories;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Edit;

public class EditTrainingCommand : ICommand<Result>
{
    [JsonIgnore] public Guid TrainingId { get; set; }
    public string Title { get; init; } = null!;
    public List<string> Topics { get; init; } = [];
    public string Description { get; init; } = null!;
    public string Prerequisites { get; init; } = null!;
    public string AcquiredSkills { get; init; } = null!;
}

public class EditTrainingCommandHandler(
    ITrainingRepository trainingRepository,
    IUniquenessTitleChecker titleChecker,
    IUnitOfWork unitOfWork)
    : ICommandHandler<EditTrainingCommand, Result>
{
    public async ValueTask<Result> Handle(
        EditTrainingCommand request,
        CancellationToken cancellationToken)
    {
        var training = await trainingRepository.GetByIdAsync(
            TrainingId.Create(request.TrainingId), cancellationToken);

        if (training == null)
        {
            return Result.Failure(ErrorCode.NotFound,
                $"Training with id `{request.TrainingId}` not found.");
        }

        var detailsResult = TrainingDetailsFactory.Create(
            request.Title, request.Description, request.Prerequisites, request.AcquiredSkills, request.Topics);

        var result = await detailsResult.MatchAsync(
            async details => await training.EditAsync(
                details.Title,
                details.Description,
                details.Prerequisites,
                details.AcquiredSkills,
                details.Topics,
                titleChecker,
                cancellationToken),
            Result.FailureAsync);

        return await result.MatchAsync<Result>(
            onSuccess: async () =>
            {
                trainingRepository.Update(training);
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
            },
            onFailure: Result.FailureAsync);
    }
}
