using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.Shared.Api.Controllers;

/// <summary>
/// Base class for the public catalogue controllers of both hosts: open to anybody, on the
/// conventional route, with the <c>[ApiController]</c> behaviours.
/// </summary>
/// <remarks>
/// A fourth base, and the first that is not about who the caller is but about the fact that there
/// is no caller. What it publishes is a read of the search index, which holds only what a visitor
/// may be shown (ADR 0050, ADR 0056, ADR 0059) — so an identity would be a question with nothing
/// to decide.
/// <para>
/// It cannot be an action on either of the other two. <see cref="ApiControllerBase"/> carries
/// <c>TrainerPolicy</c> and <see cref="AdministrationControllerBase"/> carries
/// <c>AdministratorPolicy</c>; a policy declared on an action is combined with its controller's
/// rather than replacing it, so an anonymous action hosted on either would still demand a token —
/// the same trap ADR 0054 named, arriving from the other direction.
/// </para>
/// <para>
/// It declares no 401 and no 403 because it can answer neither, which is the only interesting
/// difference from its siblings. <c>[AllowAnonymous]</c> is written out rather than left implicit:
/// a base that says nothing about authentication reads like an omission, and this one is a
/// decision.
/// </para>
/// <para>
/// <see langword="abstract"/> and <c>[ProducesErrorResponseType(typeof(void))]</c> for the reasons
/// <see cref="ApiControllerBase"/> gives at length: MVC would otherwise discover a concrete base as
/// a controller of its own, and a promised problem document nobody writes is what made a generated
/// client throw in place of a refusal.
/// </para>
/// </remarks>
[AllowAnonymous]
[ApiController]
[Route("[controller]")]
[ProducesErrorResponseType(typeof(void))]
public abstract class CatalogueControllerBase : ControllerBase
{
}
