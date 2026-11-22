using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitorer.Data;
using Monitorer.Data.Db;
using Monitorer.Data.Dto;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Controllers;

/// <summary>Agents (target VMs) and their monitoring data.</summary>
[ApiController]
[Route("api/agents")]
[Authorize]
public class AgentsController : ControllerBase
{
    private readonly AgentService _agents;
    private readonly AgentPollService _poll;
    private readonly OverviewService _overview;
    private readonly DockerOverviewService _docker;

    public AgentsController(AgentService agents, AgentPollService poll, OverviewService overview,
        DockerOverviewService docker)
    {
        _agents = agents;
        _poll = poll;
        _overview = overview;
        _docker = docker;
    }

    [HttpGet]
    public async Task<ActionResult<List<AgentDto>>> GetAll() => (await _agents.GetAll()).ToActionResult();

    /// <summary>All agents with their latest snapshot plus all live checks.</summary>
    [HttpGet("overview")]
    public async Task<ActionResult<OverviewDto>> Overview() => (await _overview.Get()).ToActionResult();

    /// <summary>All agents with their containers (Docker view).</summary>
    [HttpGet("docker")]
    public async Task<ActionResult<List<DockerHostDto>>> Docker() => (await _docker.Get()).ToActionResult();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AgentDetailDto>> Get(int id) => (await _agents.GetDetail(id)).ToActionResult();

    [HttpPost]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<AgentDto>> Create(AgentDto dto) => (await _agents.Create(dto)).ToActionResult();

    [HttpPut("{id:int}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<AgentDto>> Update(int id, AgentDto dto) => (await _agents.Update(id, dto)).ToActionResult();

    /// <summary>Forgets the pinned SSH host key (next connect pins the new one).</summary>
    [HttpPatch("{id:int}/host-key")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<AgentDto>> ResetHostKey(int id) => (await _agents.ResetHostKey(id)).ToActionResult();

    [HttpDelete("{id:int}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<AgentDto>> Delete(int id) => (await _agents.Delete(id)).ToActionResult();

    /// <summary>Fetches monitoring.json right now.</summary>
    [HttpPost("{id:int}/poll")]
    public async Task<ActionResult<AgentSnapshotDb>> Poll(int id, CancellationToken ct)
        => (await _poll.PollNow(id, ct)).ToActionResult();
}
