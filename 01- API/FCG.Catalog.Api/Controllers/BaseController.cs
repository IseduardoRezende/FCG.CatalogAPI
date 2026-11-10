using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Catalog.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ApiVersion(1)]
public abstract class BaseController : ControllerBase
{
}
