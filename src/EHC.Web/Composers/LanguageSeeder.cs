using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace EHC.Web.Composers;

/// <summary>
/// First-run seed: Arabic (ar-SA) default + mandatory, English (en-US) optional.
/// Runs only while ar-SA does not exist, so later edits in the backoffice are never overridden.
/// </summary>
public sealed class LanguageSeeder(ILanguageService languages, IRuntimeState runtime, ILogger<LanguageSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private const string Arabic = "ar-SA";
    private const string English = "en-US";

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtime.Level != RuntimeLevel.Run) return;
        if (await languages.GetAsync(Arabic) is not null) return;

        var user = Constants.Security.SuperUserKey;

        var created = await languages.CreateAsync(new Language(Arabic, "Arabic (Saudi Arabia)") { IsDefault = true, IsMandatory = true }, user);
        if (!created.Success)
        {
            logger.LogWarning("Could not create language {Iso}: {Status}", Arabic, created.Status);
            return;
        }

        var english = await languages.GetAsync(English);
        if (english is null)
        {
            await languages.CreateAsync(new Language(English, "English (United States)") { IsMandatory = false }, user);
        }
        else if (english.IsMandatory)
        {
            english.IsMandatory = false;
            await languages.UpdateAsync(english, user);
        }

        logger.LogInformation("Seeded languages: {Arabic} (default, mandatory), {English}", Arabic, English);
    }
}
