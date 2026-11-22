using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Data.Result;
using Monitorer.Extensions;
using Monitorer.Services;

namespace Monitorer.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly SessionCookieService _session;
    private readonly IAntiforgery _antiforgery;

    public AuthController(AuthService auth, SessionCookieService session, IAntiforgery antiforgery)
    {
        _auth = auth;
        _session = session;
        _antiforgery = antiforgery;
    }

    /// <summary>Login for API clients: returns the JWT (use it as "Authorization: Bearer …"). 200, 401, 403, 406, 423.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthDto>> Login(LoginDto dto)
        => (await _auth.Login(dto, HttpContext.Connection.RemoteIpAddress?.ToString())).ToActionResult();

    /// <summary>Ends the browser session (removes the session cookie). 200.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public ActionResult<bool> Logout()
    {
        _session.ClearSession(Response);
        return ServiceResult<bool>.Ok(true).ToActionResult();
    }

    /// <summary>The logged-in user. 200, 401.</summary>
    [HttpGet("me")]
    public ActionResult<AuthDto> Me() => ServiceResult<AuthDto>.Ok(new AuthDto
    {
        UserName = User.Identity?.Name ?? "",
        DisplayName = User.GetDisplayName(),
        Role = User.IsAdmin() ? UserRoles.Admin : UserRoles.User
    }).ToActionResult();

    /// <summary>CSRF token for script calls with the session cookie (send as X-CSRF-TOKEN header). 200.</summary>
    [HttpGet("csrf-token")]
    public ActionResult<string> CsrfToken()
        => ServiceResult<string>.Ok(_antiforgery.GetAndStoreTokens(HttpContext).RequestToken ?? "").ToActionResult();
}
