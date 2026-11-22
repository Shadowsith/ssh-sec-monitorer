using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Controllers;

/// <summary>Live checks for HTTP(S), WS(S) and MQTT(S) services.</summary>
[ApiController]
[Route("api/live-checks")]
[Authorize]
public class LiveChecksController : ControllerBase
{
    private readonly LiveCheckService _checks;
    private readonly LiveCheckRunnerService _runner;

    public LiveChecksController(LiveCheckService checks, LiveCheckRunnerService runner)
    {
        _checks = checks;
        _runner = runner;
    }

    [HttpGet]
    public async Task<ActionResult<List<LiveCheckDto>>> GetAll() => (await _checks.GetAll()).ToActionResult();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LiveCheckDto>> Get(int id) => (await _checks.Get(id)).ToActionResult();

    [HttpPost]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<LiveCheckDto>> Create(LiveCheckDto dto) => (await _checks.Create(dto)).ToActionResult();

    [HttpPut("{id:int}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<LiveCheckDto>> Update(int id, LiveCheckDto dto)
        => (await _checks.Update(id, dto)).ToActionResult();

    [HttpDelete("{id:int}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<LiveCheckDto>> Delete(int id) => (await _checks.Delete(id)).ToActionResult();

    /// <summary>Runs the check right now.</summary>
    [HttpPost("{id:int}/run")]
    public async Task<ActionResult<LiveCheckDto>> Run(int id, CancellationToken ct)
        => (await _runner.RunNow(id, ct)).ToActionResult();
}
