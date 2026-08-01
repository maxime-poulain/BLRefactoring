using BLRefactoring.Shared.Common;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Application.Factories;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Edit;

/// <summary>
/// Replaces the profile of a trainer. The whole profile is replaced, so a
/// <see langword="null"/> <see cref="Bio"/> clears the current one.
/// </summary>
public class EditTrainerCommand : ICommand<Result>
{
    /// <summary>
    /// The trainer being edited, resolved from the caller's token by the API layer.
    /// </summary>
    public Guid TrainerId { get; init; }

    /// <summary>
    /// The version the caller read the profile at, taken from the <c>If-Match</c> header by the
    /// API layer.
    /// </summary>
    public byte[] ExpectedVersion { get; init; } = [];

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

        if (!trainer.IsAtVersion(request.ExpectedVersion))
        {
            return Result.Failure(ErrorCode.ConcurrencyConflict, ConcurrencyMessage);
        }

        var profileResult = TrainerProfileFactory.Create(
            request.Firstname, request.Lastname, request.ContactEmail, request.Bio);

        return await profileResult.MatchAsync<Result>(
            onSuccess: async profile =>
            {
                // The aggregate raises one domain event per attribute that actually
                // changed; their handlers run during SaveChangesAsync, before persistence.
                trainer.Edit(profile.Name, profile.ContactEmail, profile.Bio);

                trainerRepository.Update(trainer);
                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (ConcurrencyConflictException)
                {
                    // A concurrent request slipped past the version pre-check; the
                    // concurrency token is the authoritative guard, so a lost race
                    // is the same business failure as a detected stale version.
                    return Result.Failure(ErrorCode.ConcurrencyConflict, ConcurrencyMessage);
                }

                return Result.Success();
            },
            onFailure: Result.FailureAsync);
    }

    private const string ConcurrencyMessage =
        "The trainer was modified by someone else since it was read. Reload it and try again.";
}
