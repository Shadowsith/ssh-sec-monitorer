using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Monitorer.Filters;

/// <summary>
/// CSRF protection for the REST API: when an unsafe request (POST/PUT/PATCH/DELETE) is authenticated
/// by the session cookie, it must carry the antiforgery token in the X-CSRF-TOKEN header.
/// Requests with an Authorization header (API clients, Swagger) are not CSRF-prone and are skipped.
/// Razor Pages validate their form tokens themselves.
/// </summary>
public class ApiAntiforgeryFilter : IAsyncAuthorizationFilter
{
    private readonly IAntiforgery _antiforgery;
    private readonly AuthSettings _auth;

    public ApiAntiforgeryFilter(IAntiforgery antiforgery, IOptions<AuthSettings> auth)
    {
        _antiforgery = antiforgery;
        _auth = auth.Value;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor)
            return;
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method))
            return;
        if (request.Headers.ContainsKey("Authorization") || !request.Cookies.ContainsKey(_auth.CookieName))
            return;

        try
        {
            await _antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new ObjectResult("Invalid or missing CSRF token.") { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
