using BLRefactoring.Shared;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Application.Factories;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Edit;

public sealed class EditTrainingCommand : ICommand<Result>
{
    public Guid TrainingId { get; init; }

    /// <summary>
    /// The version the caller read the training at, taken from the <c>If-Match</c> header by the
    /// API layer.
    /// </summary>
    public byte[] ExpectedVersion { get; init; } = [];
    public string Title { get; init; } = null!;
    public List<string> Topics { get; init; } = [];
    public string Description { get; init; } = null!;
    public string Prerequisites { get; init; } = null!;
    public string AcquiredSkills { get; init; } = null!;
}

public sealed class EditTrainingCommandHandler(
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
            return Result.Failure(ErrorCodes.NotFound,
                $"Training with id `{request.TrainingId}` not found.");
        }

        if (!training.IsAtVersion(request.ExpectedVersion))
        {
            return Result.Failure(ErrorCodes.ConcurrencyConflict, ConcurrencyMessage);
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
                    return Result.Failure(TrainingErrorCodes.DuplicateTitle,
                        "A training with the same title already exists for this trainer.");
                }
                catch (ConcurrencyConflictException)
                {
                    // Same layering for the version: the concurrency token is the
                    // authoritative guard behind the pre-check above.
                    return Result.Failure(ErrorCodes.ConcurrencyConflict, ConcurrencyMessage);
                }
                return Result.Success();
            },
            onFailure: Result.FailureAsync);
    }

    private const string ConcurrencyMessage =
        "The training was modified by someone else since it was read. Reload it and try again.";
}
