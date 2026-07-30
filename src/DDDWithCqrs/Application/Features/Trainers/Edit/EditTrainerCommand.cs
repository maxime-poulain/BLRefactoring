using System.Text.Json.Serialization;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.Messages;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Edit;

/// <summary>
/// Replaces the profile of a trainer. The whole profile is replaced, so a
/// <see langword="null"/> <see cref="Bio"/> clears the current one.
/// </summary>
public class EditTrainerCommand : ICommand<Result>
{
    /// <summary>
    /// The trainer being edited. Taken from the caller's token rather than the
    /// request body, hence never bound from JSON.
    /// </summary>
    [JsonIgnore] public Guid TrainerId { get; set; }

    public string Firstname { get; init; } = null!;
    public string Lastname { get; init; } = null!;

    /// <summary>
    /// The address at which the trainer wishes to be contacted. Editing it has no
    /// effect on the identity account used to sign in.
    /// </summary>
    public string ContactEmail { get; init; } = null!;

    public string? Bio { get; init; }
}

public class EditTrainerCommandHandler(
    ITrainerRepository trainerRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<EditTrainerCommand, Result>
{
    public async ValueTask<Result> Handle(EditTrainerCommand request, CancellationToken cancellationToken)
    {
        var trainer = await trainerRepository.GetByIdAsync(
            TrainerId.Create(request.TrainerId), cancellationToken);

        if (trainer is null)
        {
            return Result.Failure(ErrorCode.NotFound,
                $"Trainer with id `{request.TrainerId}` could not be found.");
        }

        var editionMessage = new TrainerEditionMessage
        {
            Firstname = request.Firstname,
            Lastname = request.Lastname,
            ContactEmail = request.ContactEmail,
            Bio = request.Bio
        };

        // The aggregate raises one domain event per attribute that actually changed;
        // their handlers run during SaveChangesAsync, before persistence.
        return await trainer.Edit(editionMessage).MatchAsync<Result>(
            onSuccess: async () =>
            {
                trainerRepository.Update(trainer);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success();
            },
            onFailure: Result.FailureAsync);
    }
}
