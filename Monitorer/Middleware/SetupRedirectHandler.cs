using Monitorer.Services;

namespace Monitorer.Middleware;

/// <summary>
/// As long as no admin exists (and LDAP is disabled), every page request is redirected to /Setup
/// and API calls are answered with 405 "not allowed right now".
/// </summary>
public class SetupRedirectHandler
{
    private static readonly string[] AlwaysAllowed = ["/Setup", "/Error", "/Language", "/api/setup", "/api/health", "/lib", "/css", "/js", "/favicon.ico"];

    private readonly RequestDelegate _next;

    public SetupRedirectHandler(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, SetupService setup)
    {
        var path = context.Request.Path;
        if (AlwaysAllowed.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase))
            || !await setup.IsSetupRequired())
        {
            await _next(context);
            return;
        }

        if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            await context.Response.WriteAsync("Initial setup required: create an admin user first.");
            return;
        }
        context.Response.Redirect("/Setup");
    }
}
