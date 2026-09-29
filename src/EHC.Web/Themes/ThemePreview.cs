using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using Umbraco.Extensions;
using EHC.Web.Site;

namespace EHC.Web.Themes;

/// <summary>
/// Theme preview for editors: any page with <c>?previewTheme={theme key}</c> is shown in that theme, but only for a
/// signed-in backoffice user — everyone else gets the normal page. Previews are never cached.
/// A Theme node's "Save and preview" lands here too (Views/themePreview.cshtml redirects to the homepage).
/// </summary>
public static class ThemePreview
{
    public const string QueryKey = "previewTheme";
    internal const string ItemKey = "ehc:preview-theme";

    public static void Add(IUmbracoBuilder builder)
        => builder.Services.Configure<UmbracoPipelineOptions>(o => o.AddFilter(new UmbracoPipelineFilter("EhcThemePreview")
        {
            PostRouting = app => app.Use(async (context, next) =>
            {
                if (Guid.TryParse(context.Request.Query[QueryKey], out var key)
                    && !SecurityHeaders.IsBackoffice(context.Request.Path)
                    && (await context.AuthenticateBackOfficeAsync()).Succeeded)
                {
                    context.Items[ItemKey] = key;
                    context.Response.OnStarting(() =>
                    {
                        context.Response.Headers.CacheControl = "no-store, private";
                        context.Response.Headers["X-Robots-Tag"] = "noindex";
                        return Task.CompletedTask;
                    });
                }
                await next(context);
            }),
        }));
}
