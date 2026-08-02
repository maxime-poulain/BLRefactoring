using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Application.Pagination;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetMine;
using BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator.Behaviors;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Behaviors;

public sealed class NoTrackingDuringQueryExecutionBehaviorTests : IDisposable
{
    private readonly TrainingContext _context;

    public NoTrackingDuringQueryExecutionBehaviorTests()
    {
        var options = new DbContextOptionsBuilder<TrainingContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TrainingContext(options);
    }

    [Fact]
    public async Task Handle_Query_SetsNoTrackingDuringExecution()
    {
        var behavior = new NoTrackingDuringQueryExecutionBehavior<GetMyTrainingsQuery, PagedResult<TrainingDto>>(_context);
        var query = new GetMyTrainingsQuery();

        QueryTrackingBehavior? trackingDuringExecution = null;

        MessageHandlerDelegate<GetMyTrainingsQuery, PagedResult<TrainingDto>> next = (_, _) =>
        {
            trackingDuringExecution = _context.ChangeTracker.QueryTrackingBehavior;
            return new ValueTask<PagedResult<TrainingDto>>(new PagedResult<TrainingDto>([], Page: 1, PageSize: 20, TotalCount: 0));
        };

        await behavior.Handle(query, next, CancellationToken.None);

        trackingDuringExecution.Should().Be(QueryTrackingBehavior.NoTracking);
    }

    [Fact]
    public async Task Handle_Query_RestoresOriginalTrackingAfterExecution()
    {
        _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        var behavior = new NoTrackingDuringQueryExecutionBehavior<GetMyTrainingsQuery, PagedResult<TrainingDto>>(_context);
        var query = new GetMyTrainingsQuery();

        MessageHandlerDelegate<GetMyTrainingsQuery, PagedResult<TrainingDto>> next =
            (_, _) => new ValueTask<PagedResult<TrainingDto>>(new PagedResult<TrainingDto>([], Page: 1, PageSize: 20, TotalCount: 0));

        await behavior.Handle(query, next, CancellationToken.None);

        _context.ChangeTracker.QueryTrackingBehavior.Should().Be(QueryTrackingBehavior.TrackAll);
    }

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

    public void Dispose()
    {
        _context.Dispose();
    }
}
