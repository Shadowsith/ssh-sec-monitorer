using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitorer.Data.Dto;
using Monitorer.Services;

namespace Monitorer.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly AuthService _auth;
    private readonly SessionCookieService _session;
    private readonly LdapService _ldap;

    public LoginModel(AuthService auth, SessionCookieService session, LdapService ldap)
    {
        _auth = auth;
        _session = session;
        _ldap = ldap;
    }

    [BindProperty]
    public LoginDto Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public bool LdapEnabled => _ldap.Enabled;

    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? LocalRedirect(SafeReturnUrl) : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await _auth.Login(Input, HttpContext.Connection.RemoteIpAddress?.ToString());
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            Input.Password = "";
            return Page();
        }
        _session.SetSession(Response, result.Value!);
        return LocalRedirect(SafeReturnUrl);
    }

    private string SafeReturnUrl =>
        !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) && !ReturnUrl.StartsWith("/Login", StringComparison.OrdinalIgnoreCase)
            ? ReturnUrl
            : "/";
}
