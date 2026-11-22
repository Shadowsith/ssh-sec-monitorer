using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitorer.Services;

namespace Monitorer.Pages;

/// <summary>POST only (with antiforgery token) - a GET link could be abused to log users out.</summary>
[AllowAnonymous]
public class LogoutModel : PageModel
{
    private readonly SessionCookieService _session;

    public LogoutModel(SessionCookieService session) => _session = session;

    public IActionResult OnGet() => RedirectToPage("/Index");

    public IActionResult OnPost()
    {
        _session.ClearSession(Response);
        return RedirectToPage("/Login");
    }
}
