using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Pagination;
using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.Create;
using TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetMine;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetById;
using TrainingHub.DDDWithCqrs.Infrastructure.ThirdParty.Mediator.Behaviors;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Moq;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Behaviors;

/// <summary>
/// Behaviour covered for <c>ValidationPipelineBehavior</c>.
/// </summary>
public sealed class ValidationPipelineBehaviorTests
{
    private static Mock<IValidator<TRequest>> RejectingValidator<TRequest>(params ValidationFailure[] failures)
    {
        var validator = new Mock<IValidator<TRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<TRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        return validator;
    }

    // Match is the only way into a Result, by design, so a test that wants to look at the errors
    // asks for them the same way a controller does.
    private static IReadOnlyList<Error> ErrorsOf(Result result) =>
        result.Match<IReadOnlyList<Error>>(() => [], errors => errors.ToArray());

    // -- Command without validator --

    /// <summary>
    /// Handle, command without validator, throws invalid operation exception.
    /// </summary>
    [Fact]
    public async Task Handle_CommandWithoutValidator_ThrowsInvalidOperationException()
    {
        var behavior = new ValidationPipelineBehavior<CreateTrainerCommand, Result>(
            Enumerable.Empty<IValidator<CreateTrainerCommand>>());

        var command = new CreateTrainerCommand
        {
            Firstname = "John",
            Lastname = "Doe",
            ContactEmail = "john@example.com"
        };

        MessageHandlerDelegate<CreateTrainerCommand, Result> next =
            (_, _) => new ValueTask<Result>(Result.Success());

        Func<Task> act = async () => await behavior.Handle(command, next, CancellationToken.None);

        // Still an exception, and deliberately: a command reaching the pipeline with no validator is
        // a wiring mistake, not something a caller did. Answering it with a 400 would tell the
        // caller their request was wrong when the request was fine.
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must have a validator*");
    }

    // -- Command with validation errors --

    /// <summary>
    /// Handle, command with validation errors, returns a failed result.
    /// </summary>
    [Fact]
    public async Task Handle_CommandWithValidationErrors_ReturnsAFailedResult()
    {
        var validator = RejectingValidator<CreateTrainerCommand>(
            new ValidationFailure("Email", "Email is required"));

        var behavior = new ValidationPipelineBehavior<CreateTrainerCommand, Result>(
            new[] { validator.Object });

        MessageHandlerDelegate<CreateTrainerCommand, Result> next =
            (_, _) => new ValueTask<Result>(Result.Success());

        var result = await behavior.Handle(new CreateTrainerCommand(), next, CancellationToken.None);

        // The rejection travels as a Result, so it leaves the API through the one place a business
        // failure becomes a body — under `domainErrors`, like every other failure of this stack.
        var errors = ErrorsOf(result);
        errors.Should().ContainSingle();
        errors[0].ErrorCode.Should().Be(ErrorCodes.Validation);

        // The validator's message is passed through untouched: it is what names the offending
        // field now that the field map is gone.
        errors[0].ErrorMessage.Should().Be("Email is required");
    }

    /// <summary>
    /// Handle, command with validation errors, does not reach the handler.
    /// </summary>
    [Fact]
    public async Task Handle_CommandWithValidationErrors_DoesNotReachTheHandler()
    {
        var validator = RejectingValidator<CreateTrainerCommand>(
            new ValidationFailure("Email", "Email is required"));

        var behavior = new ValidationPipelineBehavior<CreateTrainerCommand, Result>(
            new[] { validator.Object });

        var nextCalled = false;
        MessageHandlerDelegate<CreateTrainerCommand, Result> next = (_, _) =>
        {
            nextCalled = true;
            return new ValueTask<Result>(Result.Success());
        };

        await behavior.Handle(new CreateTrainerCommand(), next, CancellationToken.None);

        // The point the move from throwing to returning must not cost: the handler is still never
        // entered, so nothing is written and there is nothing to unwind.
        nextCalled.Should().BeFalse();
    }

    /// <summary>
    /// Handle, command with several validation errors, reports them all.
    /// </summary>
    [Fact]
    public async Task Handle_CommandWithSeveralValidationErrors_ReportsThemAll()
    {
        var validator = RejectingValidator<CreateTrainerCommand>(
            new ValidationFailure("Firstname", "Firstname is required"),
            new ValidationFailure("Lastname", "Lastname is required"));

        var behavior = new ValidationPipelineBehavior<CreateTrainerCommand, Result>(
            new[] { validator.Object });

        MessageHandlerDelegate<CreateTrainerCommand, Result> next =
            (_, _) => new ValueTask<Result>(Result.Success());

        var result = await behavior.Handle(new CreateTrainerCommand(), next, CancellationToken.None);

        // Everything that was wrong, in one answer — the property the domain's own factories have,
        // and which a caller fixing a form depends on.
        ErrorsOf(result).Select(error => error.ErrorMessage)
            .Should().BeEquivalentTo(new[] { "Firstname is required", "Lastname is required" });
    }

    // -- Command with valid data --

    /// <summary>
    /// Handle, command with valid data, calls next handler.
    /// </summary>
    [Fact]
    public async Task Handle_CommandWithValidData_CallsNextHandler()
    {
        var validator = new Mock<IValidator<CreateTrainerCommand>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateTrainerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var behavior = new ValidationPipelineBehavior<CreateTrainerCommand, Result>(
            new[] { validator.Object });

        var command = new CreateTrainerCommand
        {
            Firstname = "John",
            Lastname = "Doe",
            ContactEmail = "john@example.com"
        };

        var nextCalled = false;
        MessageHandlerDelegate<CreateTrainerCommand, Result> next = (_, _) =>
        {
            nextCalled = true;
            return new ValueTask<Result>(Result.Success());
        };

        await behavior.Handle(command, next, CancellationToken.None);

        nextCalled.Should().BeTrue();
    }

    // -- Query rejections have no Result to travel in --

    /// <summary>
    /// Handle, query with validation errors, still throws.
    /// </summary>
    [Fact]
    public async Task Handle_QueryWithValidationErrors_StillThrows()
    {
        var validator = RejectingValidator<GetTrainerByIdQuery>(
            new ValidationFailure("Id", "'Id' must not be empty."));

        var behavior = new ValidationPipelineBehavior<GetTrainerByIdQuery, TrainerDto?>(
            new[] { validator.Object });

        MessageHandlerDelegate<GetTrainerByIdQuery, TrainerDto?> next =
            (_, _) => new ValueTask<TrainerDto?>((TrainerDto?)null);

        Func<Task> act = async () =>
            await behavior.Handle(new GetTrainerByIdQuery(Guid.Empty), next, CancellationToken.None);

        // A query answers with the data it read, and TrainerDto has no failed state to return. The
        // exception is what ValidationExceptionHandler turns into a 400 — without it, an empty
        // identifier reaches EntityId.Create, which throws, and the caller gets a 500.
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle(failure => failure.PropertyName == "Id");
    }

    // -- Query without validator does not throw --

    /// <summary>
    /// Handle, query without validator, does not throw.
    /// </summary>
    [Fact]
    public async Task Handle_QueryWithoutValidator_DoesNotThrow()
    {
        var behavior = new ValidationPipelineBehavior<GetMyTrainingsQuery, PagedResult<TrainingDto>>(
            Enumerable.Empty<IValidator<GetMyTrainingsQuery>>());

        var query = new GetMyTrainingsQuery();
        var expected = new PagedResult<TrainingDto>([], Page: 1, PageSize: 20, TotalCount: 0);

        MessageHandlerDelegate<GetMyTrainingsQuery, PagedResult<TrainingDto>> next =
            (_, _) => new ValueTask<PagedResult<TrainingDto>>(expected);

        var result = await behavior.Handle(query, next, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }
}
