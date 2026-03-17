using System.Text.Json.Serialization;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Messages;

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
    IUniquenessTitleChecker titleChecker)
    : ICommandHandler<EditTrainingCommand, Result>
{
    public async ValueTask<Result> Handle(
        EditTrainingCommand request,
        CancellationToken cancellationToken)
    {
        var training = await trainingRepository.GetByIdAsync(
            (TrainingId)request.TrainingId, cancellationToken);

        if (training == null)
        {
            return Result.Failure(ErrorCode.NotFound,
                $"Training with id `{request.TrainingId}` not found.");
        }

        var editionMessage = new TrainingEditionMessage
        {
            Title = request.Title,
            Description = request.Description,
            Prerequisites = request.Prerequisites,
            AcquiredSkills = request.AcquiredSkills,
            Topics = request.Topics
        };

        var result = await training.EditAsync(editionMessage, titleChecker, cancellationToken);

        return await result.MatchAsync<Result>(
            onSuccess: async () =>
            {
                await trainingRepository.SaveAsync(training, cancellationToken);
                return Result.Success();
            },
            onFailure: Result.FailureAsync);
    }
}
