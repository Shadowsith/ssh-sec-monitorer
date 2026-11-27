using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Controllers;

/// <summary>CVE checks (keywords searched in BSI, TL;DR CVE, OpenCVE, opencve-kb) and the CVEs they found.</summary>
[ApiController]
[Route("api/cve-checks")]
[Authorize]
public class CveChecksController : ControllerBase
{
    private readonly CveCheckService _checks;
    private readonly CveCheckRunnerService _runner;
    private readonly CveFindingService _findings;

    public CveChecksController(CveCheckService checks, CveCheckRunnerService runner, CveFindingService findings)
    {
        _checks = checks;
        _runner = runner;
        _findings = findings;
    }

    [HttpGet]
    public async Task<ActionResult<List<CveCheckDto>>> GetAll() => (await _checks.GetAll()).ToActionResult();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CveCheckDto>> Get(int id) => (await _checks.Get(id)).ToActionResult();

    [HttpPost]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<CveCheckDto>> Create(CveCheckDto dto) => (await _checks.Create(dto)).ToActionResult();

    [HttpPut("{id:int}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<CveCheckDto>> Update(int id, CveCheckDto dto)
        => (await _checks.Update(id, dto)).ToActionResult();

    [HttpDelete("{id:int}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<CveCheckDto>> Delete(int id) => (await _checks.Delete(id)).ToActionResult();

    /// <summary>Runs the check right now.</summary>
    [HttpPost("{id:int}/run")]
    public async Task<ActionResult<CveCheckDto>> Run(int id, CancellationToken ct)
        => (await _runner.RunNow(id, ct)).ToActionResult();

    /// <summary>Found CVEs, open ones first (max. 200).</summary>
    [HttpGet("findings")]
    public async Task<ActionResult<List<CveFindingDto>>> GetFindings([FromQuery] bool baseline = false)
        => (await _findings.GetForPage(baseline)).ToActionResult();

    /// <summary>Acknowledges a CVE in all checks that found it (ends its alarm).</summary>
    [HttpPost("findings/{cveId}/acknowledge")]
    public async Task<ActionResult<int>> Acknowledge(string cveId)
        => (await _findings.Acknowledge(cveId, User.GetDisplayName())).ToActionResult();
}
