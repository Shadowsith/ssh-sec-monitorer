using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Controllers;

/// <summary>SSH users / keys for the agent hosts. Secrets are write-only.</summary>
[ApiController]
[Route("api/ssh-credentials")]
[Authorize(Roles = UserRoles.Admin)]
public class SshCredentialsController : ControllerBase
{
    private readonly SshCredentialService _credentials;

    public SshCredentialsController(SshCredentialService credentials) => _credentials = credentials;

    [HttpGet]
    public async Task<ActionResult<List<SshCredentialDto>>> GetAll() => (await _credentials.GetAll()).ToActionResult();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SshCredentialDto>> Get(int id) => (await _credentials.Get(id)).ToActionResult();

    [HttpPost]
    public async Task<ActionResult<SshCredentialDto>> Create(SshCredentialDto dto)
        => (await _credentials.Create(dto)).ToActionResult();

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SshCredentialDto>> Update(int id, SshCredentialDto dto)
        => (await _credentials.Update(id, dto)).ToActionResult();

    /// <summary>
    /// Tests a credential against a host (host reachable, host key, login, SFTP, optional monitoring.json).
    /// With credentialId, empty secrets use the stored ones. 200 (incl. failed steps), 404, 406.
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult<SshConnectionTestResultDto>> Test(SshConnectionTestDto dto, CancellationToken ct)
        => (await _credentials.Test(dto, ct)).ToActionResult();

    /// <summary>Public key (authorized_keys line) of the entered or stored private key - for the VM setup script. 200, 404, 406.</summary>
    [HttpPost("public-key")]
    public async Task<ActionResult<string>> PublicKey(SshPublicKeyRequestDto dto)
        => (await _credentials.GetPublicKey(dto)).ToActionResult();

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<SshCredentialDto>> Delete(int id) => (await _credentials.Delete(id)).ToActionResult();
}
