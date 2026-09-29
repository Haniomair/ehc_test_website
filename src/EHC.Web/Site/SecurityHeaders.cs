using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace EHC.Web.Site;

/// <summary>
/// Security headers for the public site. The Content-Security-Policy is not applied to the Umbraco backoffice
/// (/umbraco), which ships its own. Scripts and style sheets are same-origin only; inline style *attributes* are
/// allowed (theme colour variables and image focal points are rendered as validated style="--…" values).
/// Map tiles: OpenStreetMap for now — change `Tiles` when the production provider is chosen (docs/06).
/// </summary>
public static class SecurityHeaders
{
    public const string Tiles = "https://tile.openstreetmap.org";

    public static readonly string Csp = string.Join("; ",
        "default-src 'self'",
        "script-src 'self'",
        "style-src 'self'",
        "style-src-attr 'unsafe-inline'",
        $"img-src 'self' data: {Tiles}",
        "font-src 'self'",
        "media-src 'self'",
        "connect-src 'self'",
        "object-src 'none'",
        "base-uri 'self'",
        "form-action 'self'",
        "frame-ancestors 'self'",
        "upgrade-insecure-requests");

    public static void Use(IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;
            h.XContentTypeOptions = "nosniff";
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            h["Permissions-Policy"] = "geolocation=(self), camera=(), microphone=(), payment=(), usb=()";
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            if (!IsBackoffice(context.Request.Path))
            {
                h.XFrameOptions = "SAMEORIGIN";
                h.ContentSecurityPolicy = context.Request.IsHttps ? Csp : Csp.Replace("; upgrade-insecure-requests", "");
            }
            return Task.CompletedTask;
        });
        await next(context);
    });

    public static bool IsBackoffice(PathString path) =>
        path.StartsWithSegments("/umbraco", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/App_Plugins", StringComparison.OrdinalIgnoreCase);
}
