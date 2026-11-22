using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitorer.Data.Result;
using Monitorer.Extensions;

namespace Monitorer.Controllers;

[ApiController]
[Route("api/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    /// <summary>Liveness probe for Docker / reverse proxies. 200.</summary>
    [HttpGet]
    public ActionResult<string> Get() => ServiceResult<string>.Ok("ok").ToActionResult();
}
