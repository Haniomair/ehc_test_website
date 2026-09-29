using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace EHC.Web.Site;

/// <summary>
/// Brotli/gzip for pages, CSS/JS/SVG/JSON/XML, and long-lived caching for versioned front-end assets
/// (asp-append-version adds ?v=hash, so a changed file gets a new URL). The backoffice is left alone.
/// </summary>
public static class Performance
{
    public static void Add(IUmbracoBuilder builder)
    {
        builder.Services.AddResponseCompression(o =>
        {
            // public pages carry no secrets/anti-forgery tokens, so compression over HTTPS is safe here (BREACH)
            o.EnableForHttps = true;
            o.Providers.Add<BrotliCompressionProvider>();
            o.Providers.Add<GzipCompressionProvider>();
            o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["image/svg+xml", "application/xml", "text/xml"]);
        });
        builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
        builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

        builder.Services.Configure<UmbracoPipelineOptions>(o => o.AddFilter(new UmbracoPipelineFilter("EhcPerformance")
        {
            PrePipeline = app =>
            {
                app.UseWhen(c => !SecurityHeaders.IsBackoffice(c.Request.Path), a => a.UseResponseCompression());
                app.Use(async (context, next) =>
                {
                    var path = context.Request.Path;
                    if (path.StartsWithSegments("/assets"))
                    {
                        var versioned = context.Request.Query.ContainsKey("v");
                        context.Response.OnStarting(() =>
                        {
                            if (context.Response.StatusCode == StatusCodes.Status200OK)
                            {
                                context.Response.Headers.CacheControl = versioned || path.StartsWithSegments("/assets/fonts")
                                    ? "public, max-age=31536000, immutable"
                                    : "public, max-age=86400";
                            }
                            return Task.CompletedTask;
                        });
                    }
                    await next(context);
                });
            },
        }));
    }
}
