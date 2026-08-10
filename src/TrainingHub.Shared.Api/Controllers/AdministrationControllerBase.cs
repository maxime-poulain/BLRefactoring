using TrainingHub.Shared.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.Shared.Api.Controllers;

/// <summary>
/// Base class for the administrative controllers of both hosts: behind
/// <see cref="AdministratorPolicy"/>, on the conventional route, with the <c>[ApiController]</c>
/// behaviors.
/// </summary>
/// <remarks>
/// A third base rather than a fourth attribute on the second, and the reason is ADR 0051's design
/// arriving at its consequence. <see cref="ApiControllerBase"/> now carries
/// <see cref="TrainerPolicy"/>, and an administrator's token carries no <c>trainer_id</c>; a policy
/// declared on an action is combined with the class's rather than replacing it, so an administrative
/// action hosted on a trainer-facing controller would demand both and be reachable by nobody.
/// <para>
/// This is the one place this API groups by authority rather than by resource — and deliberately,
/// because that is what ADR 0051 says an administrator is. The administration gets a URL space, not
/// a model: no aggregate of its own and no vocabulary of its own. <c>AdministrationController</c>
/// holds the six actions that drive <c>Trainer</c> and <c>Training</c> through the same application
/// layer as everything else; <c>OutboxController</c> holds the two that drive no aggregate at all,
/// because what they administer is the platform's own delivery table (ADR 0061). Both sit under
/// <c>/Administration</c>, and the segment after it says which.
/// </para>
/// <para>
/// <see langword="abstract"/>, the 401, the 403 and <c>[ProducesErrorResponseType(typeof(void))]</c>
/// are here for the reasons <see cref="ApiControllerBase"/> gives at length: MVC would otherwise
/// discover a concrete base as a controller, the two statuses are properties of every action rather
/// than of any one of them, and the authorization middleware writes them with no body at all.
/// </para>
/// </remarks>
[Authorize(Policy = AdministratorPolicy.Name)]
[ApiController]
[Route("[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesErrorResponseType(typeof(void))]
public abstract class AdministrationControllerBase : ControllerBase
{
}
