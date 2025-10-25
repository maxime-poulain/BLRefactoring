using BLRefactoring.Shared;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Delete;

/// <summary>
/// Deletes a <see cref="Trainer"/> by its <see cref="Trainer.Id"/>.
/// </summary>
public class DeleteTrainerCommand : ICommand<Result>
{
    public Guid Id { get; set; }

    public DeleteTrainerCommand()
    {
    }

    public DeleteTrainerCommand(Guid id)
    {
        Id = id;
    }
}

public class DeleteTrainerCommandHandler : ICommandHandler<DeleteTrainerCommand, Result>
{
    private readonly ITrainerRepository _trainerRepository;
    private readonly ITransactionManager _transactionManager;

    public DeleteTrainerCommandHandler(
        ITrainerRepository trainerRepository,
        ITransactionManager transactionManager)
    {
        _trainerRepository = trainerRepository;
        _transactionManager = transactionManager;
    }

    public async ValueTask<Result> Handle(DeleteTrainerCommand request, CancellationToken cancellationToken)
    {
        var trainer = await _trainerRepository.GetByIdAsync(request.Id, cancellationToken);

        if (trainer == null)
        {
            return Result.Failure(ErrorCode.NotFound, $"Trainer with id `{request.Id}` does not exist");
        }

        // A domain event will be dispatched here.
        // The DomainEvent handler associated will delete
        // the trainer's trainings.
        // Therefore we need a transaction.
        // Since the operation happens in the same bounded context,
        // techniques such saga does not apply here.
        // We need to use a transaction.
        // There are alternative to create it explicitly.
        // For example a transaction might be created by a middleware.
        // This, however would be applied to queries as well.
        // We could have an attribute at the endpoint level that would
        // instruct a transaction should be created during the execution.
        trainer.MarkForDeletion();
        try
        {
            await _transactionManager.BeginTransactionAsync(cancellationToken);
            await _trainerRepository.DeleteAsync(trainer, cancellationToken);
            await _transactionManager.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await _transactionManager.RollBackAsync(cancellationToken);

            // When an unmanaged exception is thrown.
            // I prefer make it bubble up to the Api layer.
            throw;
        }

        return Result.Success();
    }
}
