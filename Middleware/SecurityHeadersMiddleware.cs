namespace CRLFruitstandESS.Middleware
{
    /// <summary>
    /// Adds security-related HTTP response headers to every response.
    ///
    /// Headers applied:
    ///   X-Frame-Options          — prevents clickjacking (DENY: no iframes allowed)
    ///   X-Content-Type-Options   — prevents MIME-type sniffing
    ///   X-XSS-Protection         — enables legacy browser XSS filter
    ///   Referrer-Policy          — limits referrer info sent to external sites
    ///   Permissions-Policy       — disables unused browser features (camera, mic, etc.)
    ///   Content-Security-Policy  — restricts resource origins to prevent XSS/injection
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            // Prevent the page from being embedded in an iframe (clickjacking)
            headers["X-Frame-Options"] = "DENY";

            // Prevent browsers from MIME-sniffing the content type
            headers["X-Content-Type-Options"] = "nosniff";

            // Legacy XSS filter (still respected by some older browsers)
            headers["X-XSS-Protection"] = "1; mode=block";

            // Only send the origin (no path/query) when navigating to external sites
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Disable browser features not needed by this app
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            // Content Security Policy:
            //   default-src 'self'          — only load resources from same origin by default
            //   script-src  'self' + CDNs   — allow Chart.js and Bootstrap Icons from jsDelivr
            //   style-src   'self' + CDNs + 'unsafe-inline' — inline styles used in Razor views
            //   img-src     'self' data:    — allow data URIs for inline images
            //   font-src    'self' + CDNs   — Bootstrap Icons font files
            //   connect-src 'self' PayMongo — allow PayMongo API calls
            //   frame-ancestors 'none'      — belt-and-suspenders clickjacking protection
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' https://cdn.jsdelivr.net 'unsafe-inline'; " +
                "style-src 'self' https://cdn.jsdelivr.net 'unsafe-inline'; " +
                "img-src 'self' data: https:; " +
                "font-src 'self' https://cdn.jsdelivr.net; " +
                "connect-src 'self' https://api.paymongo.com; " +
                "frame-ancestors 'none';";

            await _next(context);
        }
    }

    /// <summary>Extension method for clean registration in Program.cs.</summary>
    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
            => app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
