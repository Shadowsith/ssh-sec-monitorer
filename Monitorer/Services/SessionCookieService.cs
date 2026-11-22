using Microsoft.Extensions.Options;
using Monitorer.Data.Dto;

namespace Monitorer.Services;

/// <summary>Stores the JWT of the browser session in an HttpOnly, SameSite=Strict cookie.</summary>
public class SessionCookieService
{
    private readonly AuthSettings _auth;

    public SessionCookieService(IOptions<AuthSettings> auth) => _auth = auth.Value;

    public void SetSession(HttpResponse response, AuthDto auth) =>
        response.Cookies.Append(_auth.CookieName, auth.Token, CreateOptions(response, auth.ExpiresAt));

    public void ClearSession(HttpResponse response) =>
        response.Cookies.Delete(_auth.CookieName, CreateOptions(response, null));

    private CookieOptions CreateOptions(HttpResponse response, DateTime? expires) => new()
    {
        HttpOnly = true,
        // with AllowHttp the cookie is still Secure whenever the request came in via HTTPS
        Secure = !_auth.AllowHttp || response.HttpContext.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
        Expires = expires
    };
}
