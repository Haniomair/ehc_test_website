using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using EHC.Web.Site;

namespace EHC.Web.Composers;

/// <summary>
/// Once: gives the three healthy living articles taken from the current site (ehc.med.sa) the photos they have there
/// (SeedPhotos, "Health library" media folder), with alt text in both languages; only articles without an image.
/// Articles with unpublished changes keep them as a draft (ContentChanges).
/// </summary>
public sealed class HealthArticleImagesSeeder(
    IContentService contents, IContentTypeService contentTypes, IMediaService media, MediaFileManager files, MediaUrlGeneratorCollection urlGenerators,
    IShortStringHelper shortStrings, IContentTypeBaseServiceProvider typeProvider, IKeyValueService keyValues, IRuntimeState runtime, ILogger<HealthArticleImagesSeeder> logger)
    : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private const string DoneKey = "Ehc.HealthArticles.Images";
    private const string Ar = BlockJson.Ar, En = BlockJson.En;

    internal sealed record Photo(string EnglishNameStart, string File, string AltAr, string AltEn);

    internal static readonly Photo[] Photos =
    [
        new("Chronic Disease Early Detection", "health-chronic-disease-early-detection.jpg", "عائلة تزور مريضًا مبتسمًا في غرفته بالمستشفى", "A family visiting a smiling patient in his hospital room"),
        new("Breastfeeding", "health-breastfeeding.jpg", "أم تحمل طفلها الرضيع وتبتسم له", "A mother holding up her smiling baby"),
        new("Fast Healthy", "health-fast-healthy.jpg", "عائلة تجتمع في مجلس مزيّن بفوانيس رمضان", "A family together in a room decorated with Ramadan lanterns"),
    ];

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        if (runtime.Level != RuntimeLevel.Run || keyValues.GetValue(DoneKey) is not null) return;
        try
        {
            var articleType = contentTypes.Get("healthArticle");
            if (articleType is null) return;
            var articles = contents.GetPagedOfTypes([articleType.Id], 0, 500, out _, null, null).Where(a => !a.Trashed).ToList();
            var photos = new SeedPhotos(media, files, urlGenerators, shortStrings, typeProvider);
            var set = 0;
            foreach (var photo in Photos)
            {
                var article = articles.FirstOrDefault(a => (a.GetCultureName(En) ?? "").StartsWith(photo.EnglishNameStart, StringComparison.OrdinalIgnoreCase));
                if (article is null || !SeedPhotos.IsEmpty(article, "image")) continue;
                var image = photos.Import(photo.File, "Health library", article.GetCultureName(En) ?? photo.File);
                if (image is null) { logger.LogWarning("Seed photo {File} is missing from the build", photo.File); continue; }
                var hadDraft = article.Edited;
                article.SetValue("image", SeedPhotos.PickerValue(image));
                article.SetValue("imageAlt", photo.AltAr, Ar);
                article.SetValue("imageAlt", photo.AltEn, En);
                ContentChanges.SaveKeepingDrafts(contents, article, hadDraft, logger, "Photo added");
                set++;
            }
            keyValues.SetValue(DoneKey, set.ToString());
            if (set > 0) logger.LogInformation("Health library: photos added to {Count} articles", set);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Adding photos to health articles failed");
        }
    }
}
