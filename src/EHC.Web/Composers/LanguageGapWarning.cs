using EHC.Web.Blocks;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace EHC.Web.Composers;

/// <summary>
/// On publish: a warning (never a block) when sections, slides or cards on the page have text in one language but not
/// in the other, or are shown in one language only. Points editors to "Fill empty … from …" in the page's menu.
/// </summary>
public sealed class LanguageGapWarning(IContentTypeService contentTypes) : INotificationHandler<ContentPublishingNotification>
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase) { ["ar-SA"] = "Arabic", ["en-US"] = "English" };

    public void Handle(ContentPublishingNotification notification)
    {
        foreach (var page in notification.PublishedEntities)
        {
            if (!page.ContentType.VariesByCulture()) continue;
            var cultures = page.AvailableCultures.ToList();
            if (cultures.Count < 2) continue;

            var gaps = page.Properties
                .Where(p => !p.PropertyType.VariesByCulture() && p.PropertyType.PropertyEditorAlias is Constants.PropertyEditors.Aliases.BlockList or Constants.PropertyEditors.Aliases.BlockGrid)
                .SelectMany(p => BlockLanguages.Gaps(p.GetValue() as string, cultures))
                .ToList();
            if (gaps.Count == 0) continue;

            var parts = gaps.GroupBy(g => g.Culture).Select(g =>
            {
                var kinds = g.GroupBy(x => x.ContentTypeKey).Select(k => $"{contentTypes.Get(k.Key)?.Name ?? "block"} ×{k.Count()}");
                return $"{Names.GetValueOrDefault(g.Key, g.Key)}: {g.Count()} ({string.Join(", ", kinds)})";
            });
            notification.Messages.Add(new EventMessage(
                $"\"{page.Name}\": some blocks are missing a language",
                $"Not shown or without text in — {string.Join("; ", parts)}. Use \"Fill empty … from …\" in the page's ⋯ menu, then translate.",
                EventMessageType.Warning));
        }
    }
}
