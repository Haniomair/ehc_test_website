using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace EHC.Web.Blocks;

/// <summary>
/// Floating cards in a hero Right panel, with the look the editor chose. Every option maps through an allow-list to a
/// fixed class (.fc-* in frontend/src/css/ehc.css); unknown or empty values give the default.
/// </summary>
public sealed record FloatingCards(IReadOnlyList<IPublishedElement> Cards, string Style, string Motion, string Entrance, string Layout, bool HoverGlow)
{
    private static readonly Dictionary<string, string> Styles = new() { ["Glass"] = "glass", ["Clear glass"] = "clear", ["Tinted glass"] = "tinted", ["Solid"] = "solid" };
    private static readonly Dictionary<string, string> Motions = new() { ["None"] = "none", ["Float"] = "float", ["Drift"] = "drift", ["Sway"] = "sway" };
    private static readonly Dictionary<string, string> Entrances = new() { ["None"] = "none", ["Rise in"] = "rise", ["Slide in"] = "slide" };
    private static readonly Dictionary<string, string> Layouts = new() { ["Stacked"] = "stacked", ["Scattered"] = "scattered" };

    public static FloatingCards Create(IReadOnlyList<IPublishedElement> cards, string? style, string? motion, string? entrance, string? layout, bool hoverGlow) => new(
        cards,
        Pick(Styles, style, "glass"),
        Pick(Motions, motion, "none"),
        Pick(Entrances, entrance, "none"),
        Pick(Layouts, layout, "stacked"),
        hoverGlow);

    /// <summary>From a "Floating cards" Right panel item; only cards with a title.</summary>
    public static FloatingCards From(IPublishedElement item) => Create(
        item.Value<BlockListModel>("cards")?.Select(c => c.Content).Where(c => !string.IsNullOrWhiteSpace(c.Value<string>("title"))).ToList() ?? [],
        item.Value<string>("cardStyle"), item.Value<string>("motion"), item.Value<string>("entrance"), item.Value<string>("layout"),
        item.Value<bool>("hoverGlow"));

    /// <summary>Classes for the list: fc-list fc-{style} fc-motion-{motion} fc-enter-{entrance} fc-layout-{layout} [fc-hover].</summary>
    public string ListClass => $"fc-list fc-{Style} fc-motion-{Motion} fc-enter-{Entrance} fc-layout-{Layout}" + (HoverGlow ? " fc-hover" : "");

    private static string Pick(Dictionary<string, string> map, string? value, string fallback) =>
        value is not null && map.TryGetValue(value.Trim(), out var key) ? key : fallback;
}
