using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetAdministered;

/// <summary>
/// Checks <see cref="GetAdministeredTrainersQuery"/> before any handler sees it.
/// </summary>
/// <remarks>
/// The status only, and for the reason ADR 0046 gives about identifiers: the boundary refuses an
/// unknown name at model binding for a caller arriving over HTTP, and this rule refuses it for
/// anything that reaches <c>IQueryDispatcher</c> by another road. Without it the handler would hand
/// an unknown name to <c>TrainerStatus.FromName</c>, which throws by design — a <c>500</c> where a
/// refusal belongs.
/// <para>
/// The names come from <see cref="TrainerStatus.GetStatuses"/> rather than being listed here, so a
/// third standing is filterable the day it is declared and this file has nothing to remember
/// (ADR 0041).
/// </para>
/// <para>
/// The term is not checked at all. Its bound is a boundary concern — a caller should be told which
/// parameter was too long — and its meaning is the repository's: blank is no filter. There is no
/// third rule for this layer to hold.
/// </para>
/// </remarks>
public sealed class GetAdministeredTrainersQueryValidator : AbstractValidator<GetAdministeredTrainersQuery>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public GetAdministeredTrainersQueryValidator()
    {
        RuleFor(query => query.Status)
            .Must(BeAKnownStatus)
            .WithMessage("No trainer status answers to that name.");
    }

    private static bool BeAKnownStatus(string? status) =>
        string.IsNullOrWhiteSpace(status)
        || TrainerStatus.GetStatuses().Any(known => known.Name.Equals(status, StringComparison.Ordinal));
}
