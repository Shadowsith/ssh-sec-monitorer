using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Controllers;

/// <summary>TLS certificate checks (validity / days left of the certificate behind host:port).</summary>
[ApiController]
[Route("api/cert-checks")]
[Authorize]
public class CertChecksController : ControllerBase
{
    private readonly CertCheckService _checks;
    private readonly CertCheckRunnerService _runner;

    public CertChecksController(CertCheckService checks, CertCheckRunnerService runner)
    {
        _checks = checks;
        _runner = runner;
    }

    [HttpGet]
    public async Task<ActionResult<List<CertCheckDto>>> GetAll() => (await _checks.GetAll()).ToActionResult();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CertCheckDto>> Get(int id) => (await _checks.Get(id)).ToActionResult();

    [HttpPost]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<CertCheckDto>> Create(CertCheckDto dto) => (await _checks.Create(dto)).ToActionResult();

    [HttpPut("{id:int}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<CertCheckDto>> Update(int id, CertCheckDto dto)
        => (await _checks.Update(id, dto)).ToActionResult();

    [HttpDelete("{id:int}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<CertCheckDto>> Delete(int id) => (await _checks.Delete(id)).ToActionResult();

    /// <summary>Runs the check right now.</summary>
    [HttpPost("{id:int}/run")]
    public async Task<ActionResult<CertCheckDto>> Run(int id, CancellationToken ct)
        => (await _runner.RunNow(id, ct)).ToActionResult();
}
