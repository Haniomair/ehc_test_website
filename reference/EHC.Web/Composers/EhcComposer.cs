// REFERENCE CODE — port, build, fix.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using EHC.Web.Themes;

namespace EHC.Web.Composers;

public sealed class EhcComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IThemeResolver, ThemeResolver>();

        // Phase 3: builder.AddNotificationHandler<ContentSavingNotification, ThemeContrastGuard>();
        // Phase 5: builder.Services.AddHttpClient<IErWaitTimeProvider, ...>();
    }
}
