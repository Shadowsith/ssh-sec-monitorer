using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Monitorer.Data.Dto;
using Monitorer.Services;

namespace Monitorer.Pages;

/// <summary>Initial setup: creates the first admin user (only while no user exists and LDAP is off).</summary>
[AllowAnonymous]
public class SetupModel : PageModel
{
    private readonly SetupService _setup;
    private readonly IStringLocalizer _l;

    public SetupModel(SetupService setup, IOptions<AuthSettings> auth, IStringLocalizer l)
    {
        _setup = setup;
        _l = l;
        MinPasswordLength = auth.Value.MinPasswordLength;
    }

    [BindProperty]
    public SetupAdminDto Input { get; set; } = new() { UserName = "admin", DisplayName = "Administrator" };

    public string? ErrorMessage { get; private set; }

    public int MinPasswordLength { get; }

    public async Task<IActionResult> OnGetAsync() =>
        await _setup.IsSetupRequired() ? Page() : RedirectToPage("/Login");

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await _setup.CreateInitialAdmin(Input);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            Input.Password = Input.PasswordRepeat = "";
            return Page();
        }
        TempData["Success"] = _l["setup.created"].Value;
        return RedirectToPage("/Login");
    }
}
