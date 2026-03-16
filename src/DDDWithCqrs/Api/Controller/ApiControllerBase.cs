using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDDWithCqrs.Api.Controller;

/// <summary>
/// Base class for all API controllers.
/// </summary>
[Authorize]
[ApiController]
[Route("[controller]")]
public class ApiControllerBase : ControllerBase
{
}
