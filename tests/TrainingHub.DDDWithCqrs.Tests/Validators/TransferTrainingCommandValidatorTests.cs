using TrainingHub.DDDWithCqrs.Application.Features.Trainings.Transfer;
using FluentValidation.TestHelper;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Validators;

/// <summary>
/// Behaviour covered for <c>TransferTrainingCommandValidator</c>.
/// </summary>
public sealed class TransferTrainingCommandValidatorTests
{
    private readonly TransferTrainingCommandValidator _validator = new();

    /// <summary>
    /// Validate, both identifiers present, passes.
    /// </summary>
    [Fact]
    public void Validate_BothIdentifiersPresent_Passes()
    {
        var result = _validator.TestValidate(new TransferTrainingCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Validate, an empty training id, is refused.
    /// </summary>
    [Fact]
    public void Validate_AnEmptyTrainingId_IsRefused()
    {
        var result = _validator.TestValidate(new TransferTrainingCommand(Guid.Empty, Guid.NewGuid()));

        result.ShouldHaveValidationErrorFor(command => command.TrainingId);
    }

    /// <summary>
    /// Validate, an empty recipient, is refused.
    /// </summary>
    [Fact]
    public void Validate_AnEmptyRecipient_IsRefused()
    {
        var result = _validator.TestValidate(new TransferTrainingCommand(Guid.NewGuid(), Guid.Empty));

        result.ShouldHaveValidationErrorFor(command => command.RecipientTrainerId);
    }
}
