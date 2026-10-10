using System.Text.Json;
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
/// Once: the hospitals and health networks get the building photos they have on the current site (ehc.med.sa) as their
/// page header (header style "image"), from SeedPhotos into a "Facilities" media folder; a network uses the photo of its
/// main hospital. Only pages without a header image; pages with unpublished changes keep them as a draft.
/// Health centres (primary care) never get a photo: many of the buildings don't show the cluster at its best.
/// </summary>
public sealed class FacilityPhotosSeeder(
    IContentService contents, IContentTypeService contentTypes, IMediaService media, MediaFileManager files, MediaUrlGeneratorCollection urlGenerators,
    IShortStringHelper shortStrings, IContentTypeBaseServiceProvider typeProvider, IKeyValueService keyValues, IRuntimeState runtime, ILogger<FacilityPhotosSeeder> logger)
    : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private const string DoneKey = "Ehc.Facilities.Photos";
    private const string NoCentreKey = "Ehc.Facilities.Photos.NoCentres";
    private const string En = BlockJson.En;

    /// <summary>English page name → photo file (Composers/SeedMedia/facility-{slug}.jpg).</summary>
    internal static readonly Dictionary<string, string> Photos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dammam Medical Complex"] = "dammam-medical-complex",
        ["Dhahran Long Term Center"] = "dhahran-long-term-center",
        ["Qatif Central Hospital"] = "qatif-central-hospital",
        ["Prince Mohammad Hospital for Genetic Blood Disease"] = "prince-mohammad-genetic-blood-disease",
        ["Jubail General Hospital"] = "jubail-general-hospital",
        ["Al-Khafji General Hospital"] = "al-khafji-general-hospital",
        ["Prince Sultan Hospital in Arairah"] = "prince-sultan-arairah",
        ["Salwa General Hospital"] = "salwa-general-hospital",
        ["Anak General Hospital"] = "anak-general-hospital",
        ["Ras Tanura General Hospital"] = "ras-tanura-general-hospital",
        ["Al Batha General Hospital"] = "al-batha-general-hospital",
        ["Prince Sultan Hospital in Maliga"] = "prince-sultan-maliga",
        ["Al-Rafaiah General Hospital"] = "al-rafaiah-general-hospital",
        ["Qaria AlOlya General Hospital"] = "qaria-alolya-general-hospital",
        ["Nairyah General Hospital"] = "nairyah-general-hospital",
        ["Safwa General Hospital"] = "safwa-general-hospital",
        ["Abqaiq General Hospital"] = "abqaiq-general-hospital",
        ["King Fahad Specialist Hospital"] = "king-fahad-specialist-hospital",
        ["Maternity and Children Hospital"] = "maternity-and-children-hospital",
        ["Dhahran Eye Specialist Hospital"] = "dhahran-eye-specialist-hospital",
        ["Saud Al Babtain Cardiac Center"] = "saud-al-babtain-cardiac-center",
        ["Eradah Mental Health Complex"] = "eradah-mental-health-complex",
        // health networks: the photo of their main hospital (Al Khobar has no hospital of its own: it keeps the band)
        ["Dammam Health Network"] = "dammam-medical-complex",
        ["Qatif Health Network"] = "qatif-central-hospital",
        ["Jubail Health Network"] = "jubail-general-hospital",
        ["Rural Health Network"] = "al-khafji-general-hospital",
        ["Specialized Hospitals"] = "king-fahad-specialist-hospital",
    };

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        if (runtime.Level != RuntimeLevel.Run) return;
        RemoveCentrePhotos();
        if (keyValues.GetValue(DoneKey) is not null) return;
        try
        {
            var types = new[] { "facility", "healthNetwork" }.Select(a => contentTypes.Get(a)?.Id).OfType<int>().ToArray();
            if (types.Length == 0) return;
            var photos = new SeedPhotos(media, files, urlGenerators, shortStrings, typeProvider);
            var set = 0;
            foreach (var page in contents.GetPagedOfTypes(types, 0, 2000, out _, null, null).Where(p => !p.Trashed))
            {
                var name = page.GetCultureName(En);
                if (name is null || !Photos.TryGetValue(name, out var slug) || !SeedPhotos.IsEmpty(page, "headerImage")) continue;
                if ((page.GetValue<string>("facilityType") ?? "").Contains("primaryCare")) continue;
                var file = "facility-" + slug + ".jpg";
                var image = photos.Import(file, "Facilities", Photos.First(p => p.Value == slug).Key);
                if (image is null) { logger.LogWarning("Seed photo {File} is missing from the build", file); continue; }
                var hadDraft = page.Edited;
                page.SetValue("headerImage", SeedPhotos.PickerValue(image));
                page.SetValue("headerStyle", JsonSerializer.Serialize(new[] { "image" }));
                ContentChanges.SaveKeepingDrafts(contents, page, hadDraft, logger, "Header photo added");
                set++;
            }
            keyValues.SetValue(DoneKey, set.ToString());
            if (set > 0) logger.LogInformation("Facilities: header photos added to {Count} hospitals and networks", set);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Adding facility photos failed");
        }
    }

    /// <summary>
    /// Once: the first version gave the Al Khobar Health Network the current site's "Al Khobar centres" photo, which may
    /// show a health centre. Its header goes back to the band and the photo leaves the media library (recycle bin).
    /// </summary>
    private void RemoveCentrePhotos()
    {
        if (keyValues.GetValue(NoCentreKey) is not null) return;
        try
        {
            var type = contentTypes.Get("healthNetwork");
            var page = type is null ? null : contents.GetPagedOfTypes([type.Id], 0, 100, out _, null, null)
                .FirstOrDefault(p => !p.Trashed && p.GetCultureName(En) == "Al Khobar Health Network");
            var photo = media.GetRootMedia().FirstOrDefault(m => m.Name == "Facilities") is { } folder
                ? media.GetPagedChildren(folder.Id, 0, 500, out _).FirstOrDefault(m => m.Name == "Al Khobar Health Network") : null;
            if (page is not null && photo is not null && (page.GetValue<string>("headerImage") ?? "").Contains(photo.Key.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var hadDraft = page.Edited;
                page.SetValue("headerImage", null);
                page.SetValue("headerStyle", null);
                ContentChanges.SaveKeepingDrafts(contents, page, hadDraft, logger, "Header photo removed");
            }
            if (photo is not null) media.MoveToRecycleBin(photo);
            keyValues.SetValue(NoCentreKey, "1");
        }
        catch (Exception e) { logger.LogError(e, "Removing the Al Khobar network photo failed"); }
    }
}
