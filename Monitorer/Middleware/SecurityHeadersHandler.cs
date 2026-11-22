namespace Monitorer.Middleware;

/// <summary>Adds security headers (CSP, framing, sniffing, referrer) to every response.</summary>
public class SecurityHeadersHandler
{
    // All scripts / styles are served locally from wwwroot - no inline scripts, no CDNs.
    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; " +
        "connect-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'self'; object-src 'none'";

    private readonly RequestDelegate _next;

    public SecurityHeadersHandler(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        // Swagger UI uses inline scripts / styles; it is only enabled on demand (Swagger:Enabled)
        if (!context.Request.Path.StartsWithSegments("/swagger"))
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
        return _next(context);
    }
}
