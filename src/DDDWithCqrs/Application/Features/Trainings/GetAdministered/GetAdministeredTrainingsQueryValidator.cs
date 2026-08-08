using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using FluentValidation;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetAdministered;

/// <summary>
/// Checks <see cref="GetAdministeredTrainingsQuery"/> before any handler sees it.
/// </summary>
/// <remarks>
/// The status only, for the reason its sibling gives: the boundary refuses an unknown name for a
/// caller arriving over HTTP, and this rule refuses it for anything that reaches
/// <c>IQueryDispatcher</c> by another road, where <c>TrainingStatus.FromName</c> would throw
/// instead (ADR 0046).
/// <para>
/// The names come from <see cref="TrainingStatus.GetStatuses"/> rather than being listed here — the
/// third state arrived in #98, and this file would already have been wrong (ADR 0041).
/// </para>
/// </remarks>
public sealed class GetAdministeredTrainingsQueryValidator : AbstractValidator<GetAdministeredTrainingsQuery>
{
    /// <summary>
    /// Builds the rules.
    /// </summary>
    public GetAdministeredTrainingsQueryValidator()
    {
        RuleFor(query => query.Status)
            .Must(BeAKnownStatus)
            .WithMessage("No training status answers to that name.");
    }

    private static bool BeAKnownStatus(string? status) =>
        string.IsNullOrWhiteSpace(status)
        || TrainingStatus.GetStatuses().Any(known => known.Name.Equals(status, StringComparison.Ordinal));
}
