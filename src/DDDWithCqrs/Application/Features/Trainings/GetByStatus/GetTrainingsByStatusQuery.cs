using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetByStatus;

/// <summary>
/// One page of trainings across every trainer, as the administration reads them, newest first
/// (ADR 0055).
/// </summary>
/// <remarks>
/// <c>GetTrainingsByCurrentTrainerQuery</c> answers "the caller's own" and this one answers "everybody's",
/// which is the whole difference and the reason it needed a record before it could exist. Asking
/// for <c>Withheld</c> is what it is for: nothing else in this API can find what the administration
/// has taken down.
/// <para>
/// The status travels as a string for the reason the sibling query gives: a message carries what
/// its sender put in it, and a string is what a validator can refuse.
/// </para>
/// <para>
/// It carries a term since ADR 0060, and carried none before it — for a reason that was never a
/// choice: a title mapped through a value converter is one EF Core cannot look inside, so a
/// substring match did not translate. Remapping it as a complex property is what closed the
/// asymmetry ADR 0055 recorded between the two administrative listings.
/// </para>
/// </remarks>
public sealed class GetTrainingsByStatusQuery : IQuery<PagedResult<AdministrationTrainingDto>>
{
    /// <summary>
    /// The state to narrow to, or <see langword="null"/> for every training.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// The term to look for in a title, or <see langword="null"/> for every training.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// The page asked for; the default page when none was.
    /// </summary>
    public PageRequest Paging { get; init; } = new();
}
