using Microsoft.Extensions.Logging;

namespace BLRefactoring.Shared.Infrastructure.Services;

/// <summary>
/// Deliberately fake <see cref="ITrainingSearchIndexer"/> that only logs the
/// indexing request. A real implementation would push a document to a search
/// engine (Elasticsearch, Azure AI Search…); the port keeps that concern out of
/// the application layer entirely.
/// </summary>
public sealed class FakeTrainingSearchIndexer(ILogger<FakeTrainingSearchIndexer> logger)
    : ITrainingSearchIndexer
{
    /// <inheritdoc />
    public Task IndexAsync(Guid trainingId, Guid trainerId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Indexing training `{TrainingId}` of trainer `{TrainerId}` into the search index.",
            trainingId,
            trainerId);

        return Task.CompletedTask;
    }
}
