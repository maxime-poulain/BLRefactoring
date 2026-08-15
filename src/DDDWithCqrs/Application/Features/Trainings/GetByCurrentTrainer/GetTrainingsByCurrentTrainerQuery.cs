using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Trainings.GetByCurrentTrainer;

/// <summary>
/// One page of the calling trainer's own trainings, newest first.
/// </summary>
/// <remarks>
/// It carries nothing but its paging, and that is the whole design: there is no trainer to pass,
/// so there is no trainer a caller could pass wrongly. The handler resolves the identity through
/// <c>ICurrentUserService</c>, the way <c>CreateTrainingCommandHandler</c> already does for the
/// trainer a new training belongs to.
/// <para>
/// The alternative — a <c>TrainerId</c> property filled by the action from the token — puts the
/// tenancy decision in the one layer that has forty other things to do, and makes it a value in
/// flight rather than a fact. Every future caller of this query would then be a place the wrong
/// identifier could enter, and no test of the read side would notice.
/// </para>
/// <para>
/// The paging is held rather than inherited: this query used to derive a query-side
/// <c>PagedQuery</c> base, which dissolved into the kernel's <see cref="PageRequest"/> when both
/// hosts started paging (ADR 0029). The default matters — a query built in code with no paging
/// named asks for the default page, never for the whole table.
/// </para>
/// </remarks>
public sealed class GetTrainingsByCurrentTrainerQuery : IQuery<PagedResult<TrainingDto>>
{
    /// <summary>
    /// The page asked for; the default page when none was.
    /// </summary>
    public PageRequest Paging { get; init; } = new();
}
