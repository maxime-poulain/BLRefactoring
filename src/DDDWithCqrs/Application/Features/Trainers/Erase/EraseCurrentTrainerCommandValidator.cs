using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.Erase;

/// <summary>
/// Validates <see cref="EraseCurrentTrainerCommand"/>.
/// </summary>
/// <remarks>
/// The command carries nothing — it means "erase whoever is calling", and the caller comes from
/// the token rather than the body. There is therefore nothing to check, and this class exists to
/// say that out loud: the pipeline demands a validator per command, and a missing one throws at
/// dispatch rather than at build. An empty one is the difference between "nothing to validate"
/// and "nobody got round to it".
/// </remarks>
public sealed class EraseCurrentTrainerCommandValidator : AbstractValidator<EraseCurrentTrainerCommand>;
