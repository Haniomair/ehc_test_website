using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace EHC.Web.Site;

/// <summary>Saving changes that seeders make to content editors already work on.</summary>
public static class ContentChanges
{
    /// <summary>
    /// Saves <paramref name="item"/> and republishes it only when it was published without unpublished edits
    /// (<paramref name="hadDraft"/> = item.Edited read before changing it); otherwise the change stays a draft, so an
    /// editor's unfinished work is never published by a seeder.
    /// </summary>
    public static void SaveKeepingDrafts(IContentService contents, IContent item, bool hadDraft, ILogger logger, string what)
    {
        contents.Save(item);
        if (hadDraft || !item.Published)
        {
            logger.LogWarning("{What} in {Name}: saved as a draft because it had unpublished changes; publish it in the backoffice", what, item.Name);
            return;
        }
        var cultures = item.ContentType.VariesByCulture() ? item.PublishedCultures.ToArray() : ["*"];
        var result = contents.Publish(item, cultures);
        logger.LogInformation("{What} in {Name} ({Status})", what, item.Name, result.Result);
    }
}
