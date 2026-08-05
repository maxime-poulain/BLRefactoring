using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.Transfer;

/// <summary>
/// Checks <see cref="TransferTrainingCommand"/> before any handler sees it.
/// <para>
/// Runs in the pipeline behaviour, so a rejected message never reaches the domain and
/// the caller gets one document listing every field at fault rather than the first.
/// </para>
/// </summary>
public sealed class TransferTrainingCommandValidator : AbstractValidator<TransferTrainingCommand>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public TransferTrainingCommandValidator()
    {
        RuleFor(command => command.TrainingId).NotEmpty();
        RuleFor(command => command.RecipientTrainerId).NotEmpty();
    }
}
