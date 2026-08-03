using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.SetPhoto;

/// <summary>
/// Validates <see cref="SetTrainerPhotoCommand"/>.
/// </summary>
/// <remarks>
/// One rule, and deliberately only one. Whether the bytes are an image, which image, and whether
/// there are too many of them are the aggregate's rules — read off the content itself, and
/// answered with codes the aggregate owns. Restating any of them here would give the same refusal
/// two different codes depending on which check happened to run first, and the pipeline's
/// validation code means "rejected before the domain saw it", which would then stop being true.
/// </remarks>
public sealed class SetTrainerPhotoCommandValidator : AbstractValidator<SetTrainerPhotoCommand>
{
    /// <summary>
    /// Refuses a command with no payload at all, which is a malformed request rather than a bad photo.
    /// </summary>
    public SetTrainerPhotoCommandValidator()
    {
        RuleFor(command => command.Content).NotNull();
    }
}
