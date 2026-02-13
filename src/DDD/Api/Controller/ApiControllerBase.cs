using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDD.Api.Controller;

[Authorize]
[ApiController]
[Route("[controller]")]
public class ApiControllerBase : ControllerBase
{
}
