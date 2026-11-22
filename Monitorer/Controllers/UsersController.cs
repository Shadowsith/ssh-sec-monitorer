using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = UserRoles.Admin)]
public class UsersController : ControllerBase
{
    private readonly UserService _users;

    public UsersController(UserService users) => _users = users;

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll() => (await _users.GetAll()).ToActionResult();

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(UserCreateDto dto) => (await _users.Create(dto)).ToActionResult();

    [HttpPatch("{id:int}/password")]
    public async Task<ActionResult<UserDto>> UpdatePassword(int id, UserPasswordDto dto)
        => (await _users.UpdatePassword(id, dto.Password)).ToActionResult();

    /// <summary>Changes the role of a local user. 202, 404, 405 (LDAP user / own user / last admin), 406.</summary>
    [HttpPatch("{id:int}/role")]
    public async Task<ActionResult<UserDto>> UpdateRole(int id, UserRoleDto dto)
        => (await _users.UpdateRole(id, dto.Role, User.GetUserId())).ToActionResult();

    [HttpPatch("{id:int}/active")]
    public async Task<ActionResult<UserDto>> UpdateActive(int id, [FromBody] bool isActive)
        => (await _users.UpdateActive(id, isActive, User.GetUserId())).ToActionResult();

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<UserDto>> Delete(int id) => (await _users.Delete(id, User.GetUserId())).ToActionResult();
}
