using AwesomeAssertions;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetAll;
using BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator.Behaviors;
using BLRefactoring.Shared.Common.Results;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Moq;
using Xunit;

namespace BLRefactoring.DDDWithCqrs.Tests.Behaviors;

public class ValidationPipelineBehaviorTests
{
    // -- Command without validator --

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

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must have a validator*");
    }

    // -- Command with validation errors --

    [Fact]
    public async Task Handle_CommandWithValidationErrors_ThrowsValidationException()
    {
        var validator = new Mock<IValidator<CreateTrainerCommand>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateTrainerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Email", "Email is required")
            }));

        var behavior = new ValidationPipelineBehavior<CreateTrainerCommand, Result>(
            new[] { validator.Object });

        var command = new CreateTrainerCommand();

        MessageHandlerDelegate<CreateTrainerCommand, Result> next =
            (_, _) => new ValueTask<Result>(Result.Success());

        Func<Task> act = async () => await behavior.Handle(command, next, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle(e => e.PropertyName == "Email");
    }

    // -- Command with valid data --

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

    // -- Query without validator does not throw --

    [Fact]
    public async Task Handle_QueryWithoutValidator_DoesNotThrow()
    {
        var behavior = new ValidationPipelineBehavior<GetAllTrainersQuery, List<TrainerDto>>(
            Enumerable.Empty<IValidator<GetAllTrainersQuery>>());

        var query = new GetAllTrainersQuery();
        var expected = new List<TrainerDto>();

        MessageHandlerDelegate<GetAllTrainersQuery, List<TrainerDto>> next =
            (_, _) => new ValueTask<List<TrainerDto>>(expected);

        var result = await behavior.Handle(query, next, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }
}
