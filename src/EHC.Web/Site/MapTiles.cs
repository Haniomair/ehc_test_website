using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace EHC.Web.Site;

/// <summary>
/// Self-hosted map tiles: one PMTiles file (wwwroot/tiles, built by `npm run tiles` in /frontend) served as a static
/// file. The browser reads it with HTTP range requests, so maps make no third-party calls (docs/06-integrations.md).
/// </summary>
public static class MapTiles
{
    public const string Path = "/tiles/ehc-region.pmtiles";
    public const string ContentType = "application/vnd.pmtiles";

    /// <summary>Versioned URL of the tiles file (changes when the file is rebuilt), or null when it hasn't been built.</summary>
    public static string? Url(IWebHostEnvironment env)
    {
        var file = env.WebRootFileProvider.GetFileInfo(Path);
        return file.Exists ? $"{Path}?v={file.LastModified.ToUnixTimeSeconds()}" : null;
    }

    public static void Add(IUmbracoBuilder builder)
    {
        // static files refuse unknown extensions; range requests (206) are supported out of the box
        builder.Services.Configure<StaticFileOptions>(o =>
        {
            var types = o.ContentTypeProvider as FileExtensionContentTypeProvider ?? new FileExtensionContentTypeProvider();
            types.Mappings[".pmtiles"] = ContentType;
            o.ContentTypeProvider = types;
        });

        builder.Services.Configure<UmbracoPipelineOptions>(o => o.AddFilter(new UmbracoPipelineFilter("EhcMapTiles")
        {
            PrePipeline = app => app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/tiles"))
                {
                    var versioned = context.Request.Query.ContainsKey("v");
                    context.Response.OnStarting(() =>
                    {
                        if (context.Response.StatusCode is StatusCodes.Status200OK or StatusCodes.Status206PartialContent)
                        {
                            context.Response.Headers.CacheControl = versioned ? "public, max-age=31536000, immutable" : "public, max-age=86400";
                        }
                        return Task.CompletedTask;
                    });
                }
                await next(context);
            }),
        }));
    }
}
