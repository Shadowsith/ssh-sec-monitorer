using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Monitorer.Services;

namespace Monitorer.Pages;

/// <summary>Stores the chosen UI language in the culture cookie read by the request localization. POST only.</summary>
[AllowAnonymous]
public class LanguageModel : PageModel
{
    private readonly JsonStringLocalizer _localizer;
    private readonly AuthSettings _auth;

    public LanguageModel(JsonStringLocalizer localizer, IOptions<AuthSettings> auth)
    {
        _localizer = localizer;
        _auth = auth.Value;
    }

    public IActionResult OnGet() => RedirectToPage("/Index");

    public IActionResult OnPost(string? language, string? returnUrl)
    {
        if (language is not null && _localizer.Languages.Contains(language, StringComparer.OrdinalIgnoreCase))
            Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(language)),
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = !_auth.AllowHttp || Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                    IsEssential = true,
                    Expires = DateTimeOffset.UtcNow.AddYears(1)
                });
        return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
