using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Controllers;

/// <summary>Remembered containers: remove entries of containers that no longer exist.</summary>
[ApiController]
[Route("api/known-containers")]
[Authorize(Roles = UserRoles.Admin)]
public class KnownContainersController : ControllerBase
{
    private readonly KnownContainerService _known;

    public KnownContainersController(KnownContainerService known) => _known = known;

    /// <summary>Forgets one container. 200, 404.</summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<KnownContainerDb>> Delete(int id) => (await _known.Delete(id)).ToActionResult();

    /// <summary>Forgets all currently missing containers of an agent. 200.</summary>
    [HttpDelete("~/api/agents/{agentId:int}/known-containers/missing")]
    public async Task<ActionResult<List<KnownContainerDb>>> DeleteMissing(int agentId)
        => (await _known.DeleteMissing(agentId)).ToActionResult();
}
