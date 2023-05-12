using BLRefactoring.DDD.Application.Services.TrainingServices.Dtos;
using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.DDD.Application.Services.TrainingServices;

public interface ITrainingApplicationService
{
    Task<Result<TrainingDto>> CreateAsync(TrainingCreationRequest request);
    Task<Result<TrainingDto>> GetByIdAsync(Guid id);
    Task<List<TrainingDto>> GetAllAsync(CancellationToken cancellationToken = default);
}

public class TrainingApplicationService : ITrainingApplicationService
{
    private readonly ITrainerRepository _trainerRepository;
    private readonly IUniquenessTitleChecker _uniquenessTitleChecker;
    private readonly ITrainingRepository _trainingRepository;

    public TrainingApplicationService(
        ITrainerRepository trainerRepository,
        IUniquenessTitleChecker uniquenessTitleChecker,
        ITrainingRepository trainingRepository)
    {
        _trainerRepository = trainerRepository;
        _uniquenessTitleChecker = uniquenessTitleChecker;
        _trainingRepository = trainingRepository;
    }

    public async Task<Result<TrainingDto>> CreateAsync(TrainingCreationRequest request)
    {
        var trainer = await _trainerRepository.GetByIdAsync(request.TrainerId);

        var result = await Training.CreateAsync(
            request.Title,
            request.StartDate,
            request.EndDate,
            trainer,
            request.Rates.ToRates(),
            _uniquenessTitleChecker);

        if (!result.IsSuccess)
        {
            return Result<TrainingDto>.Failure(result.Errors);
        }

        await _trainingRepository.SaveAsync(result.Value);
        return Result<TrainingDto>.Success(result.Value.ToDto());

    }

    public async Task<Result<TrainingDto>> GetByIdAsync(Guid id)
    {
        var training = await _trainingRepository.GetByIdAsync(id);

        return training is null
            ? Result<TrainingDto>.Failure(ErrorCode.Unspecified, $"Training with id `{id}` not found.")
            : Result<TrainingDto>.Success(training.ToDto());
    }

    public async Task<List<TrainingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return (await _trainingRepository.GetAllAsync(cancellationToken)).ToDtos();
    }
}
