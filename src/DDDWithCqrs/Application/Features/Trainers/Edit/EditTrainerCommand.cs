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
public sealed class EditTrainerCommand : ICommand<Result>
{
    /// <summary>
    /// The version the caller read the profile at, taken from the <c>If-Match</c> header by the
    /// API layer.
    /// </summary>
    public byte[] ExpectedVersion { get; init; } = [];

    /// <summary>
    /// The trainer's first name, as the caller sent it.
    /// </summary>
    public string Firstname { get; init; } = null!;

    /// <summary>
    /// The trainer's last name, as the caller sent it.
    /// </summary>
    public string Lastname { get; init; } = null!;

    /// <summary>
    /// The address at which the trainer wishes to be contacted. Editing it has no
    /// effect on the identity account used to sign in.
    /// </summary>
    public string ContactEmail { get; init; } = null!;

    /// <summary>
    /// The trainer's biography, or <see langword="null"/> for none — absent at creation, cleared
    /// on edition.
    /// </summary>
    public string? Bio { get; init; }
}

/// <summary>
/// Edits the calling trainer's own profile, and only ever that one.
/// </summary>
/// <remarks>
/// The trainer is resolved here rather than carried on the command. The endpoint is
/// <c>PUT /Trainer/me</c> — there has never been a trainer for a caller to choose — and taking one
/// as a field made that a promise the API layer had to keep on every call site instead of a fact.
/// The same reasoning as <c>CreateTrainingCommandHandler</c>, which resolves the owner of a new
/// training the same way.
/// </remarks>
public sealed class EditTrainerCommandHandler(
    ITrainerRepository trainerRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<EditTrainerCommand, Result>
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    public async ValueTask<Result> Handle(EditTrainerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trainerId = currentUserService.TrainerId;

        var trainer = await trainerRepository.GetByIdAsync(
            TrainerId.Create(trainerId), cancellationToken);

        if (trainer is null)
        {
            return Result.Failure(ErrorCodes.NotFound,
                $"Trainer with id `{trainerId}` could not be found.");
        }

        if (!trainer.IsAtVersion(request.ExpectedVersion))
        {
            return Result.Failure(ErrorCodes.ConcurrencyConflict, ConcurrencyMessage);
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
                    return Result.Failure(ErrorCodes.ConcurrencyConflict, ConcurrencyMessage);
                }

                return Result.Success();
            },
            onFailure: Result.FailureAsync);
    }

    private const string ConcurrencyMessage =
        "The trainer was modified by someone else since it was read. Reload it and try again.";
}
