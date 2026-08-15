using AwesomeAssertions;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.Create;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetByCurrentTrainer;
using TrainingHub.DDDWithCqrs.Infrastructure.ThirdParty.Mediator.Behaviors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Behaviors;

/// <summary>
/// Behavior covered for <c>NoTrackingDuringQueryExecutionBehavior</c>.
/// </summary>
public sealed class NoTrackingDuringQueryExecutionBehaviorTests : IDisposable
{
    private readonly TrainingContext _context;

    /// <summary>
    /// No tracking during query execution behavior tests.
    /// </summary>
    public NoTrackingDuringQueryExecutionBehaviorTests()
    {
        var options = new DbContextOptionsBuilder<TrainingContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TrainingContext(options);
    }

    /// <summary>
    /// Handle, query, sets no tracking during execution.
    /// </summary>
    [Fact]
    public async Task Handle_Query_SetsNoTrackingDuringExecution()
    {
        var behavior = new NoTrackingDuringQueryExecutionBehavior<GetTrainingsByCurrentTrainerQuery, PagedResult<TrainingDto>>(_context);
        var query = new GetTrainingsByCurrentTrainerQuery();

        QueryTrackingBehavior? trackingDuringExecution = null;

        MessageHandlerDelegate<GetTrainingsByCurrentTrainerQuery, PagedResult<TrainingDto>> next = (_, _) =>
        {
            trackingDuringExecution = _context.ChangeTracker.QueryTrackingBehavior;
            return new ValueTask<PagedResult<TrainingDto>>(new PagedResult<TrainingDto>([], Page: 1, PageSize: 20, TotalCount: 0));
        };

        await behavior.Handle(query, next, CancellationToken.None);

        trackingDuringExecution.Should().Be(QueryTrackingBehavior.NoTracking);
    }

    /// <summary>
    /// Handle, query, restores original tracking after execution.
    /// </summary>
    [Fact]
    public async Task Handle_Query_RestoresOriginalTrackingAfterExecution()
    {
        _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        var behavior = new NoTrackingDuringQueryExecutionBehavior<GetTrainingsByCurrentTrainerQuery, PagedResult<TrainingDto>>(_context);
        var query = new GetTrainingsByCurrentTrainerQuery();

        MessageHandlerDelegate<GetTrainingsByCurrentTrainerQuery, PagedResult<TrainingDto>> next =
            (_, _) => new ValueTask<PagedResult<TrainingDto>>(new PagedResult<TrainingDto>([], Page: 1, PageSize: 20, TotalCount: 0));

        await behavior.Handle(query, next, CancellationToken.None);

        _context.ChangeTracker.QueryTrackingBehavior.Should().Be(QueryTrackingBehavior.TrackAll);
    }

    /// <summary>
    /// Handle, command, does not change tracking.
    /// </summary>
    [Fact]
    public async Task Handle_Command_DoesNotChangeTracking()
    {
        var originalBehavior = _context.ChangeTracker.QueryTrackingBehavior;

        var behavior = new NoTrackingDuringQueryExecutionBehavior<CreateTrainerCommand, Result>(_context);
        var command = new CreateTrainerCommand
        {
            Firstname = "John",
            Lastname = "Doe",
            ContactEmail = "john@example.com"
        };

        QueryTrackingBehavior? trackingDuringExecution = null;

        MessageHandlerDelegate<CreateTrainerCommand, Result> next = (_, _) =>
        {
            trackingDuringExecution = _context.ChangeTracker.QueryTrackingBehavior;
            return new ValueTask<Result>(Result.Success());
        };

        await behavior.Handle(command, next, CancellationToken.None);

        trackingDuringExecution.Should().Be(originalBehavior);
        _context.ChangeTracker.QueryTrackingBehavior.Should().Be(originalBehavior);
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    public void Dispose()
    {
        _context.Dispose();
    }
}
