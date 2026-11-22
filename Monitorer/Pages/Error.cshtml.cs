using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Monitorer.Pages;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Code { get; set; }

    public string Title => Code switch
    {
        403 => "Kein Zugriff",
        404 => "Seite nicht gefunden",
        _ => "Ein Fehler ist aufgetreten"
    };

    public void OnGet() { }

    public void OnPost() { }
}
