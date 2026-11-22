using Microsoft.Extensions.Options;
using Monitorer.Data.Dto;

namespace Monitorer.Services;

/// <summary>Stores the JWT of the browser session in an HttpOnly, SameSite=Strict cookie.</summary>
public class SessionCookieService
{
    private readonly AuthSettings _auth;

    public SessionCookieService(IOptions<AuthSettings> auth) => _auth = auth.Value;

    public void SetSession(HttpResponse response, AuthDto auth) =>
        response.Cookies.Append(_auth.CookieName, auth.Token, CreateOptions(auth.ExpiresAt));

    public void ClearSession(HttpResponse response) =>
        response.Cookies.Delete(_auth.CookieName, CreateOptions(null));

    private CookieOptions CreateOptions(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = _auth.SecureCookie,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
        Expires = expires
    };
}
