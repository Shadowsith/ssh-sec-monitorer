using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Services;

namespace Monitorer.Pages.CveChecks;

[Authorize(Roles = UserRoles.Admin)]
public class EditModel : PageModel
{
    private readonly CveCheckService _checks;
    private readonly IStringLocalizer _l;

    public EditModel(CveCheckService checks, IStringLocalizer l)
    {
        _checks = checks;
        _l = l;
    }

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public CveCheckDto Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public bool IsNew => Id is null or 0;

    public async Task<IActionResult> OnGetAsync()
    {
        if (IsNew)
        {
            Input = new CveCheckDto { Providers = [.. CveProviders.All] };
            return Page();
        }
        var result = await _checks.Get(Id!.Value);
        if (!result.IsSuccess)
            return NotFound();
        Input = result.Value!;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = IsNew ? await _checks.Create(Input) : await _checks.Update(Id!.Value, Input);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Message;
            return Page();
        }
        TempData["Success"] = _l["cveChecks.saved", result.Value!.Name].Value;
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var result = await _checks.Delete(Id ?? 0);
        TempData[result.IsSuccess ? "Success" : "Error"] =
            result.IsSuccess ? _l["cveChecks.deleted", result.Value!.Name].Value : result.Message;
        return RedirectToPage("Index");
    }
}
