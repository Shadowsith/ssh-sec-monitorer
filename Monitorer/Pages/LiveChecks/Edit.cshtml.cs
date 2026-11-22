using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitorer.Data;
using Monitorer.Data.Dto;
using Monitorer.Services;

namespace Monitorer.Pages.LiveChecks;

[Authorize(Roles = UserRoles.Admin)]
public class EditModel : PageModel
{
    private readonly LiveCheckService _checks;

    public EditModel(LiveCheckService checks) => _checks = checks;

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public LiveCheckDto Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public bool IsNew => Id is null or 0;

    public async Task<IActionResult> OnGetAsync()
    {
        if (IsNew)
            return Page();
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
            Input.MqttPassword = null;
            return Page();
        }
        TempData["Success"] = $"Live-Check \"{result.Value!.Name}\" gespeichert.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var result = await _checks.Delete(Id ?? 0);
        TempData[result.IsSuccess ? "Success" : "Error"] =
            result.IsSuccess ? $"Live-Check \"{result.Value!.Name}\" gelöscht." : result.Message;
        return RedirectToPage("Index");
    }
}
