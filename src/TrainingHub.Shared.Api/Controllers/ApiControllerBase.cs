using TrainingHub.Shared.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.Shared.Api.Controllers;

/// <summary>
/// Base class for the trainer-facing controllers of both hosts: authenticated by default, behind
/// <see cref="TrainerPolicy"/>, on the conventional route, with the <c>[ApiController]</c>
/// behaviors.
/// </summary>
/// <remarks>
/// <see langword="abstract"/> on purpose. <c>[ApiController]</c> derives from
/// <c>ControllerAttribute</c>, so MVC's discovery would otherwise pick this class up as a
/// controller in its own right — actionless and harmless, but a controller nonetheless. Being
/// abstract keeps it out of the application model, as <see cref="AuthControllerBase"/> already is.
/// <para>
/// The 401 is declared here rather than on each of the eighteen actions below, for the same reason
/// <c>[Authorize]</c> is: it is a property of every one of them. An unauthenticated call to any
/// action of either host is answered 401 by the authentication middleware, and until now the
/// document said so nowhere — so every generated client treated the one response it is guaranteed
/// to meet as an unexpected one.
/// </para>
/// <para>
/// <c>[ProducesErrorResponseType(typeof(void))]</c> is what keeps that declaration honest.
/// <c>[ApiController]</c> makes <c>ProblemDetails</c> the type of every error response whose type
/// is not stated, which is right for the failures this API writes itself and wrong for the two it
/// does not: 401 and 403 are written by the authentication middleware, with no body at all. Left to
/// the default, the document promised a problem document there and the generated client obeyed it —
/// deserialising an empty body and throwing <c>"Response was null which was not expected."</c> in
/// place of the 401. Every error body this API actually sends is declared explicitly as
/// <c>ProblemDetails</c> at its own action, so nothing else relies on the default.
/// </para>
/// <para>
/// The 403 joined it when the administrator did (ADR 0051). It used to be the answer of two
/// actions — the ones the ownership policy guards — and is now the answer of every one of them: a
/// caller whose token carries no <c>trainer_id</c> is refused here, before any action runs.
/// Declared for the same reason as the 401, and bodiless for the same reason: the authorization
/// middleware writes it, and it writes nothing.
/// </para>
/// </remarks>
[Authorize(Policy = TrainerPolicy.Name)]
[ApiController]
[Route("[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesErrorResponseType(typeof(void))]
public abstract class ApiControllerBase : ControllerBase
{
}
