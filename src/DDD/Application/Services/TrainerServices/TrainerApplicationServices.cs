using BLRefactoring.DDD.Application.Services.TrainerServices.Dto;
using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.DDD.Application.Services.TrainerServices;

public interface ITrainerApplicationService
{
    // Another possibility would have been to return just the Id of the newly created Trainer.
    Task<Result<TrainerDto>> CreateAsync(TrainerCreationRequest request);
    Task<Result<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TrainerDto[]> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class TrainerApplicationService : ITrainerApplicationService
{
    private readonly ITrainerRepository _trainerRepository;
    private readonly ITransactionManager _transactionManager;

    public TrainerApplicationService(
        ITrainerRepository trainerRepository,
        ITransactionManager transactionManager)
    {
        _trainerRepository = trainerRepository;
        _transactionManager = transactionManager;
    }

    public async Task<Result<TrainerDto>> CreateAsync(TrainerCreationRequest request)
    {
        // Trainer.Create() could have been receiving a Domain Object instead of primitive types.
        // However, for the sake of simplicity, we are using primitive types here.
        var result = Trainer.Create(request.Firstname, request.Lastname, request.Email);

        if (result.IsFailure)
        {
            return Result<TrainerDto>.Failure(result.Errors);
        }

        await _trainerRepository.SaveAsync(result.Value);
        return Result<TrainerDto>.Success(result.Value.ToDto());

    }

    public async Task<Result<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByIdAsync(id, cancellationToken);
        if (trainer is null)
        {
            return Result<TrainerDto>.Failure(ErrorCode.Unspecified, $"Trainer with id `{id}` could not be found.");
        }

        return Result<TrainerDto>.Success(trainer.ToDto());
    }

    public async Task<TrainerDto[]> GetAllAsync(CancellationToken cancellationToken)
    {
        var trainers = await _trainerRepository.GetAllAsync(cancellationToken);
        return trainers.Select(x => x.ToDto()).ToArray();
    }

    // This operation works across two different aggregates (Trainer and its Training).
    // In a typical DDD approach, each aggregate should be a transactional boundary by itself.
    // Hence, ideally, we wouldn't need a TransactionManager to coordinate the operations.
    // Instead, we use domain events to maintain consistency between aggregates.
    //
    // When a Trainer is marked for deletion, a TrainerDeletedDomainEvent is added.
    // This event, when published, can be handled to delete the associated Training.
    //
    // To ensure this, we rely on the eventual consistency provided by the domain events.
    // The Trainer is deleted first, and then the domain event is published.
    // The Training deletion occurs separately as a result of handling the published event.
    //
    // This approach does require us to handle potential failures in event handling.
    // For instance, the deletion of the Training might fail even after the Trainer is deleted.
    // To address this, we can use techniques like event retries, idempotent handlers,
    // or other fault tolerance strategies.
    //
    // Finally, the SaveChangesAsync method is designed to first persist the changes to the database
    // and then publish the domain events. Note that these two operations are not part of the same
    // transaction. This might lead to a situation where the Trainer is deleted, but the
    // TrainerDeletedDomainEvent is not published due to an error, leading to an inconsistent state.
    // An Outbox pattern can be used to ensure that the local database transaction and the event
    // publishing are treated atomically.
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByIdAsync(id, cancellationToken);

        if (trainer is null)
        {
            return Result.Failure(ErrorCode.NotFound, $"Training with id `{id}` not found.");
        }

        try
        {
            await _transactionManager.BeginTransactionAsync(cancellationToken);
            trainer.MarkForDeletion();
            await _trainerRepository.DeleteAsync(trainer, cancellationToken);
            await _transactionManager.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await _transactionManager.RollBackAsync(cancellationToken);
        }
        return Result.Success();
    }
}
