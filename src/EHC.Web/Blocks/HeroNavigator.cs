using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Extensions;

namespace EHC.Web.Blocks;

/// <summary>
/// A campaign hero with rightPanel = "navigator" shows the page's first care navigator block in its right column;
/// that block is then not rendered again as its own section. <paramref name="panelOverride"/> is a look-customizer
/// choice (Site/Customizer.cs) that replaces the hero's own rightPanel.
/// </summary>
public static class HeroNavigator
{
    public static BlockListItem? Embedded(BlockListModel? blocks, string? panelOverride = null)
    {
        if (blocks is null) return null;
        var hero = blocks.FirstOrDefault(b => b.Content.ContentType.Alias == "heroCampaignBlock");
        if (hero is null || (panelOverride ?? hero.Content.Value<string>("rightPanel")) != "navigator") return null;
        return blocks.FirstOrDefault(b => b.Content.ContentType.Alias == "careNavigatorBlock");
    }
}
