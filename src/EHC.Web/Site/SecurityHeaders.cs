using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EHC.Web.Site;

/// <summary>
/// Security headers for the public site. The Content-Security-Policy is not applied to the Umbraco backoffice
/// (/umbraco), which ships its own. Scripts and style sheets are same-origin only; inline style *attributes* are
/// allowed (theme colour variables and image focal points are rendered as validated style="--…" values).
/// Map tiles are self-hosted (MapTiles: one PMTiles file read with range requests), so no map host is allowed.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>Video section: YouTube thumbnails and the privacy-enhanced player (loaded only after the visitor presses play).</summary>
    public const string YouTubeImages = "https://i.ytimg.com";
    public const string YouTubePlayer = "https://www.youtube-nocookie.com";

    /// <summary>The policy without any configured Frappe hosts (Microsoft Forms and Power BI are always allowed).</summary>
    public static string Csp => Build([]);

    /// <summary>Builds the policy; <paramref name="frameSources"/> are the Embed section's allowed frame origins.</summary>
    public static string Build(IEnumerable<string> frameSources) => string.Join("; ",
        "default-src 'self'",
        "script-src 'self'",
        "style-src 'self'",
        "style-src-attr 'unsafe-inline'",
        $"img-src 'self' data: {YouTubeImages}",
        "font-src 'self'",
        "media-src 'self'",
        $"frame-src {string.Join(' ', new[] { YouTubePlayer }.Concat(frameSources).Distinct())}",
        "connect-src 'self'",
        "object-src 'none'",
        "base-uri 'self'",
        "form-action 'self'",
        "frame-ancestors 'self'",
        "upgrade-insecure-requests");

    public static void Use(IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmbedOptions>>().Value;
        var csp = Build(Embed.FrameSources(options));
        var cspHttp = csp.Replace("; upgrade-insecure-requests", "");
        var noIndex = app.ApplicationServices.GetRequiredService<IConfiguration>().GetValue<bool>("Ehc:Seo:NoIndex");
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var h = context.Response.Headers;
                h.XContentTypeOptions = "nosniff";
                h["Referrer-Policy"] = "strict-origin-when-cross-origin";
                h["Permissions-Policy"] = "geolocation=(self), camera=(), microphone=(), payment=(), usb=()";
                h["Cross-Origin-Opener-Policy"] = "same-origin";
                if (noIndex)
                {
                    h["X-Robots-Tag"] = "noindex, nofollow";
                }
                if (!IsBackoffice(context.Request.Path))
                {
                    h.XFrameOptions = "SAMEORIGIN";
                    h.ContentSecurityPolicy = context.Request.IsHttps ? csp : cspHttp;
                }
                return Task.CompletedTask;
            });
            await next(context);
        });
    }

    public static bool IsBackoffice(PathString path) =>
        path.StartsWithSegments("/umbraco", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/App_Plugins", StringComparison.OrdinalIgnoreCase);
}
