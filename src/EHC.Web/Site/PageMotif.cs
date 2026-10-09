using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace EHC.Web.Site;

/// <summary>
/// The decoration of an inner page's header band: a colour role for its section and an icon for its emblem.
/// The icon is the page's own (specialties, e-services), else the one an editor gave its mega-menu link, else a
/// default for its type, else its nearest ancestor's menu icon. No icon: the band shows only the pattern.
/// </summary>
public sealed record PageMotif(string? Icon, string Tone)
{
    /// <summary>Tone: brand | support | accent | highlight, the <c>data-tone</c> values the page band styles in ehc.css.</summary>
    public static PageMotif For(IPublishedContent page, BlockListModel? megaMenu)
    {
        var links = MenuIcons(megaMenu);
        var icon = Clean(page.Value<string>("icon"))
                   ?? links.GetValueOrDefault(page.Key)
                   ?? TypeIcon(page)
                   ?? page.Ancestors().Select(a => links.GetValueOrDefault(a.Key)).FirstOrDefault(i => i is not null);
        return new(icon, ToneFor(page.ContentType.Alias));
    }

    private static string ToneFor(string alias) => alias switch
    {
        "newsFolder" or "newsItem" => "accent",
        "campaignFolder" or "campaign" => "highlight",
        "healthLibrary" or "healthArticle" or "healthToolsFolder" or "healthTool" or "specialtyFolder" or "specialty" => "support",
        _ => "brand",
    };

    private static string? TypeIcon(IPublishedContent page) => page.ContentType.Alias switch
    {
        "facility" => page.Value<string>("facilityType") == "primaryCare" ? "clinic" : "hosp",
        "facilityFolder" or "healthNetwork" => "hosp",
        "doctorFolder" or "doctor" => "doc",
        "specialtyFolder" or "specialty" => "heart",
        "eServicesFolder" or "eService" => "app",
        "healthLibrary" or "healthArticle" => "book",
        "healthToolsFolder" or "healthTool" => "target",
        "newsFolder" or "newsItem" => "news",
        "campaignFolder" or "campaign" => "flag",
        _ => null,
    };

    /// <summary>Page key → icon of the first mega-menu link that points at that page.</summary>
    private static Dictionary<Guid, string> MenuIcons(BlockListModel? megaMenu)
    {
        var map = new Dictionary<Guid, string>();
        foreach (var top in megaMenu ?? Enumerable.Empty<BlockListItem>())
        {
            foreach (var col in top.Content.Value<BlockListModel>("columns") ?? Enumerable.Empty<BlockListItem>())
            {
                foreach (var l in col.Content.Value<BlockListModel>("links") ?? Enumerable.Empty<BlockListItem>())
                {
                    if (l.Content.Value<Link>("link")?.Udi is GuidUdi udi && Clean(l.Content.Value<string>("icon")) is { } icon && icon != "er")
                    {
                        map.TryAdd(udi.Guid, icon);
                    }
                }
            }
        }
        return map;
    }

    /// <summary>Icon names are sprite ids: lower-case letters, digits and dashes only.</summary>
    private static string? Clean(string? icon) =>
        !string.IsNullOrWhiteSpace(icon) && icon.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-') ? icon : null;
}
