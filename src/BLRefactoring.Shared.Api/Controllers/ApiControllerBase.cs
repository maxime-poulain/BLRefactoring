using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.Shared.Api.Controllers;

/// <summary>
/// Base class for the API controllers of both hosts: authenticated by default, on the
/// conventional route, with the <c>[ApiController]</c> behaviours.
/// </summary>
/// <remarks>
/// <see langword="abstract"/> on purpose. <c>[ApiController]</c> derives from
/// <c>ControllerAttribute</c>, so MVC's discovery would otherwise pick this class up as a
/// controller in its own right — actionless and harmless, but a controller nonetheless. Being
/// abstract keeps it out of the application model, as <see cref="AuthControllerBase"/> already is.
/// </remarks>
[Authorize]
[ApiController]
[Route("[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
}
