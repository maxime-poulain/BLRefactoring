using BLRefactoring.DDD.Application.Services.TrainerServices.Dto;
using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.DDD.Application.Services.TrainerServices;

public interface ITrainerApplicationService
{
    // Another possibility would have been to return just the Id of the newly created Trainer.
    Task<Result<TrainerDto>> CreateAsync(TrainerCreationRequest request);
    Task<Result<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TrainerDto[]> GetAllAsync(CancellationToken cancellationToken);
}

public sealed class TrainerApplicationService : ITrainerApplicationService
{
    private readonly ITrainerRepository _trainerRepository;

    public TrainerApplicationService(ITrainerRepository trainerRepository)
    {
        _trainerRepository = trainerRepository;
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
}
