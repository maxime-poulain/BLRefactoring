using BLRefactoring.DDDWithCqrs.Application.Pagination;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.CQS;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetMine;

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
/// </remarks>
public sealed class GetMyTrainingsQuery : PagedQuery, IQuery<PagedResult<TrainingDto>>
{
}
