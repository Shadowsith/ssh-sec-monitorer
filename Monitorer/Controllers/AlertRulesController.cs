using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Controllers;

/// <summary>Alert rules (mail alarms) and the currently active alerts.</summary>
[ApiController]
[Route("api/alert-rules")]
[Authorize(Roles = UserRoles.Admin)]
public class AlertRulesController : ControllerBase
{
    private readonly AlertRuleService _rules;
    private readonly AlertEngineService _engine;

    public AlertRulesController(AlertRuleService rules, AlertEngineService engine)
    {
        _rules = rules;
        _engine = engine;
    }

    [HttpGet]
    public async Task<ActionResult<List<AlertRuleDto>>> GetAll() => (await _rules.GetAll()).ToActionResult();

    /// <summary>Currently active alerts (incl. those still waiting for their delay).</summary>
    [HttpGet("~/api/alerts/active")]
    public async Task<ActionResult<List<ActiveAlertDto>>> Active() => (await _rules.GetActive()).ToActionResult();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AlertRuleDto>> Get(int id) => (await _rules.Get(id)).ToActionResult();

    [HttpPost]
    public async Task<ActionResult<AlertRuleDto>> Create(AlertRuleDto dto) => (await _rules.Create(dto)).ToActionResult();

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AlertRuleDto>> Update(int id, AlertRuleDto dto)
        => (await _rules.Update(id, dto)).ToActionResult();

    [HttpPatch("{id:int}/enabled")]
    public async Task<ActionResult<AlertRuleDto>> UpdateEnabled(int id, [FromBody] bool enabled)
        => (await _rules.UpdateEnabled(id, enabled)).ToActionResult();

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<AlertRuleDto>> Delete(int id) => (await _rules.Delete(id)).ToActionResult();

    /// <summary>Sends a test mail to the rule's recipients. 200, 404, 405 (mail disabled), 406, 409.</summary>
    [HttpPost("{id:int}/test")]
    public async Task<ActionResult<bool>> Test(int id, CancellationToken ct) => (await _engine.SendTest(id, ct)).ToActionResult();
}
