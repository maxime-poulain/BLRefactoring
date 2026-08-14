using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.Contact;

/// <summary>
/// Checks <see cref="ContactTrainerCommand"/> before any handler sees it.
/// </summary>
/// <remarks>
/// Two rules, and both are about identifiers. That is the whole of what a validator may say here:
/// the contract declares shape and presence at model binding, the domain judges meaning, and this
/// guards what neither can (ADR 0043). Restating the message's length bounds would be a second
/// opinion that is either dead or a divergence only one host has.
/// <para>
/// The identifiers are guarded even though the contract refuses them too, and that duplication is
/// deliberate: the contract answers a request, this answers anything that reaches
/// <c>ICommandDispatcher</c>, and the application layer never assumes the boundary checked first
/// (ADR 0046).
/// </para>
/// </remarks>
public sealed class ContactTrainerCommandValidator : AbstractValidator<ContactTrainerCommand>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public ContactTrainerCommandValidator()
    {
        RuleFor(command => command.TrainerId)
            .NotEmpty()
            .WithMessage("A message must name the trainer it is addressed to.");

        // Guarded only when there is one: this identifier is optional by design, and a guard on
        // an absent value would refuse a visitor writing from a trainer's own page (ADR 0046).
        // NotEqual rather than NotEmpty, because on a nullable NotEmpty compares to the nullable's
        // default — null — and waves the empty GUID through.
        RuleFor(command => command.TrainingId)
            .NotEqual(Guid.Empty)
            .When(command => command.TrainingId.HasValue)
            .WithMessage("A training a message names cannot be the empty identifier.");
    }
}
