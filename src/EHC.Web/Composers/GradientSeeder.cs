using EHC.Web.Blocks;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace EHC.Web.Composers;

/// <summary>
/// Creates Site settings › Gradients with the three built-in hero gradients (the old presets 0 / 1 / 2) once,
/// after the gradient document types exist. Runs only while the site settings node has no Gradients folder,
/// so editors' changes are never overridden.
/// </summary>
public sealed class GradientSeeder(IContentService contents, IContentTypeService contentTypes, IRuntimeState runtime, ILogger<GradientSeeder> logger)
    : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private static readonly (string Name, GradientDesign Design)[] Seeds =
    [
        ("EHC blue", new(135, [new(0, "Deep 900"), new(45, "Deep 800"), new(100, "Primary 500")], new("Primary 400", 80, 20, 100))),
        ("Teal", new(135, [new(0, "Support 900"), new(45, "Support 800"), new(100, "Support 500")], new("Support 400", 80, 20, 100))),
        ("Rose", new(135, [new(0, "#3A1030"), new(45, "Highlight 800"), new(100, "Highlight 500")], new("#F08AA6", 80, 20, 100))),
    ];

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        try { Run(); }
        catch (Exception e) { logger.LogError(e, "Seeding gradients failed"); }
    }

    private void Run()
    {
        if (runtime.Level != RuntimeLevel.Run) return;
        var settingsType = contentTypes.Get("siteSettings");
        var gradientType = contentTypes.Get("gradient");
        if (settingsType is null || contentTypes.Get("gradientFolder") is null || gradientType is null) return;
        if (!gradientType.PropertyTypeExists("design")) return;

        foreach (var settings in contents.GetRootContent().Where(c => c.ContentTypeId == settingsType.Id))
        {
            if (settings.Trashed) continue;
            var children = contents.GetPagedChildren(settings.Id, 0, 200, out _, propertyAliases: null, filter: null, ordering: null, loadTemplates: false);
            if (children.Any(c => c.ContentType.Alias == "gradientFolder")) continue;

            var folder = contents.Create("Gradients", settings.Key, "gradientFolder");
            contents.Save(folder);
            contents.Publish(folder, ["*"]);
            foreach (var (name, design) in Seeds)
            {
                var g = contents.Create(name, folder.Key, "gradient");
                g.SetValue("design", Gradients.Json(design));
                contents.Save(g);
                contents.Publish(g, ["*"]);
            }
            logger.LogInformation("Seeded {Count} gradients under {Settings}", Seeds.Length, settings.Name);
        }
    }
}
