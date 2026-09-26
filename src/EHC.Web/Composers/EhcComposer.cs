using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.WebEncoders;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
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
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, LanguageSeeder>();

        // Phase 3: builder.AddNotificationHandler<ContentSavingNotification, ThemeContrastGuard>();
        // Phase 5: builder.Services.AddHttpClient<IErWaitTimeProvider, ...>();
    }
}
