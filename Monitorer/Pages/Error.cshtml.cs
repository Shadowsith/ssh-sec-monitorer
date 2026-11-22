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

    public string TitleKey => Code switch
    {
        403 => "error.forbidden",
        404 => "error.notFound",
        _ => "error.general"
    };

    public void OnGet() { }

    public void OnPost() { }
}
