using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Extensions;

namespace EHC.Web.Blocks;

/// <summary>
/// A campaign hero with rightPanel = "navigator" shows the page's first care navigator block in its right column;
/// that block is then not rendered again as its own section.
/// </summary>
public static class HeroNavigator
{
    public static BlockListItem? Embedded(BlockListModel? blocks)
    {
        if (blocks is null) return null;
        var hero = blocks.FirstOrDefault(b => b.Content.ContentType.Alias == "heroCampaignBlock");
        if (hero is null || hero.Content.Value<string>("rightPanel") != "navigator") return null;
        return blocks.FirstOrDefault(b => b.Content.ContentType.Alias == "careNavigatorBlock");
    }
}
