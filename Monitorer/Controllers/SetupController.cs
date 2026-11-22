using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Controllers;

[ApiController]
[Route("api/setup")]
[AllowAnonymous]
public class SetupController : ControllerBase
{
    private readonly SetupService _setup;

    public SetupController(SetupService setup) => _setup = setup;

    /// <summary>true while no admin exists (and LDAP is disabled). 200.</summary>
    [HttpGet("required")]
    public async Task<ActionResult<bool>> Required()
        => ServiceResult<bool>.Ok(await _setup.IsSetupRequired()).ToActionResult();

    /// <summary>Creates the initial admin. 201, 405 (already done), 406, 409.</summary>
    [HttpPost("admin")]
    public async Task<ActionResult<UserDto>> CreateAdmin(SetupAdminDto dto)
        => (await _setup.CreateInitialAdmin(dto)).ToActionResult();
}
