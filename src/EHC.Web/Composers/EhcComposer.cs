using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.WebEncoders;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using EHC.Web.Site;
using EHC.Web.Themes;

namespace EHC.Web.Composers;

public sealed class EhcComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);

        // Arabic-first site: don't turn every non-Latin character into an &#x…; entity. HTML-sensitive
        // characters (< > & " ') are still encoded.
        builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

        builder.Services.AddScoped<IThemeResolver, ThemeResolver>();
        builder.Services.AddScoped<ISiteContext, SiteContext>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, LanguageSeeder>();
        builder.AddNotificationHandler<ContentSavingNotification, ThemeContrastGuard>();

        // "/" has no content of its own: send visitors to the default (Arabic) site.
        builder.Services.Configure<UmbracoPipelineOptions>(o => o.AddFilter(new UmbracoPipelineFilter("EhcRootRedirect")
        {
            PrePipeline = app => app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/" && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
                {
                    context.Response.Redirect("/ar/" + context.Request.QueryString, permanent: false);
                    return;
                }
                await next(context);
            }),
        }));

        // Phase 5: builder.Services.AddHttpClient<IErWaitTimeProvider, ...>();
    }
}
