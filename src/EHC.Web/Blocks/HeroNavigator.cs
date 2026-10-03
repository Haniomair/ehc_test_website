using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Extensions;

namespace EHC.Web.Blocks;

/// <summary>
/// Look-customizer only (Site/Customizer.cs, panel = "navigator"): the campaign hero shows the page's first care
/// navigator block in its right column, and that block is then not rendered again as its own section.
/// A care navigator added to a Right panel is a separate block and not affected.
/// </summary>
public static class HeroNavigator
{
    public static BlockListItem? Embedded(BlockListModel? blocks, string? panelOverride = null)
    {
        if (blocks is null || panelOverride != "navigator") return null;
        if (!blocks.Any(b => b.Content.ContentType.Alias == "heroCampaignBlock")) return null;
        return blocks.FirstOrDefault(b => b.Content.ContentType.Alias == "careNavigatorBlock");
    }
}
