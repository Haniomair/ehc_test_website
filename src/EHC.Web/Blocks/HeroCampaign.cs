using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace EHC.Web.Blocks;

/// <summary>The hero's right-panel settings (its Right panel blocks).</summary>
public sealed record HeroPanel(bool HasBlocks);

/// <summary>One slide's right-panel settings.</summary>
public sealed record SlidePanel(bool Hide, bool HasBlocks);

/// <summary>
/// One rendered right panel: the logo orbit behind it (<paramref name="Art"/>), Right panel blocks (logo art excluded)
/// or the page's care navigator (look-customizer only). <see cref="Slides"/> are the slides that show it.
/// </summary>
public sealed record PanelLayer(bool Art, IReadOnlyList<BlockListItem> Blocks, BlockListItem? Navigator)
{
    public List<int> Slides { get; } = [];

    public bool IsEmpty => !Art && Blocks.Count == 0 && Navigator is null;

    /// <summary>Some items are desktop only (<see cref="HeroCampaign.DesktopOnly"/>); anything else is shown on phones too.</summary>
    public bool Interactive => Navigator is not null || Blocks.Any(b => !HeroCampaign.DesktopOnly.Contains(b.Content.ContentType.Alias));
}

/// <summary>
/// Campaign hero: what each slide shows beside its text, and whether the spinning mark shows behind it.
/// Order: the slide's "Hide right panel", its own Right panel blocks, then the hero's Right panel blocks.
/// A look-customizer override (Site/Customizer.cs: art / navigator / cards / none) replaces it for every slide.
/// Old fixed settings are converted on start (Composers/HeroSettingsMigration.cs).
/// </summary>
public static class HeroCampaign
{
    /// <summary>Right panel items rendered by Views/Partials/heroPanel/{alias}.cshtml (allow-list).</summary>
    public static readonly IReadOnlySet<string> PanelItems = new HashSet<string>
    {
        "panelSearch", "panelFacility", "panelErWait", "panelQuickActions", "panelDoctor", "panelCampaign",
        "panelNotice", "panelApp", "panelEmergency", "panelCountdown", "panelMedia", "panelStats",
    };

    /// <summary>Decoration or tall media: hidden on phones (logo art is too, by its own markup).</summary>
    public static readonly IReadOnlySet<string> DesktopOnly = new HashSet<string> { "panelCards", "panelMedia" };

    /// <summary>
    /// One source per slide: "none", "slide" (its own blocks), "hero" (the hero's blocks), or with a customizer override
    /// "navigator" (the page's care navigator), "art" (logo orbit + the hero's floating cards), "cards" (the hero's
    /// floating cards). Slides with the same source share one rendered panel.
    /// </summary>
    public static IReadOnlyList<string> Sources(HeroPanel hero, IReadOnlyList<SlidePanel> slides, string? locked, bool navigatorAvailable) =>
        slides.Select(s => locked switch
        {
            "none" => "none",
            "navigator" => navigatorAvailable ? "navigator" : hero.HasBlocks ? "hero" : "none",
            "art" or "cards" => locked,
            _ => s.Hide ? "none" : s.HasBlocks ? "slide" : hero.HasBlocks ? "hero" : "none",
        }).ToList();

    /// <summary>The slides that render (a slide needs a title).</summary>
    public static List<IPublishedElement> Slides(IPublishedElement hero) =>
        hero.Value<BlockListModel>("slides")?.Select(s => s.Content).Where(s => !string.IsNullOrWhiteSpace(s.Value<string>("title"))).ToList() ?? [];

    public static HeroPanel Hero(IPublishedElement hero) => new(hero.Value<BlockListModel>("panel")?.Count > 0);

    public static SlidePanel Slide(IPublishedElement slide) => new(slide.Value<bool>("hidePanel") || IsPoster(slide), slide.Value<BlockListModel>("panel")?.Count > 0);

    /// <summary>A slide whose background picture is a finished poster with its own text ("The picture is the message").</summary>
    public static bool IsPoster(IPublishedElement slide) => slide.Value<bool>("posterMode") && slide.Value<MediaWithCrops>("backgroundImage") is not null;

    /// <summary>The whole poster (not cropped), resized to the given width.</summary>
    public static string? PosterUrl(IPublishedElement slide, int width) =>
        slide.Value<MediaWithCrops>("backgroundImage") is { } image ? image.GetCropUrl(width: width, furtherOptions: "format=webp&quality=82") ?? image.Url() : null;

    private static readonly int[] PosterWidths = [640, 960, 1280, 1920, 2560];

    /// <summary>
    /// The whole poster at several widths (srcset), so phones get a small file. Both poster images (the wide one and
    /// the one in the slide on phones) use it with the same sizes, so a phone downloads one file, not two.
    /// Null when the picture cannot be resized (then only src is used).
    /// </summary>
    public static string? PosterSrcset(IPublishedElement slide)
    {
        if (slide.Value<MediaWithCrops>("backgroundImage") is not { } image) return null;
        var urls = PosterWidths.Select(w => (w, url: image.GetCropUrl(width: w, furtherOptions: "format=webp&quality=82"))).ToList();
        return urls.Any(u => u.url is null) ? null : string.Join(", ", urls.Select(u => $"{u.url} {u.w}w"));
    }

    /// <summary>
    /// The panels to render, one per distinct source, each listing the slides that show it (so a panel the hero
    /// shares with several slides is rendered once: no duplicate forms or ids).
    /// </summary>
    public static List<PanelLayer> Layers(IPublishedElement hero, IReadOnlyList<IPublishedElement> slides, IReadOnlyList<string> sources, BlockListItem? navigator)
    {
        var layers = new List<PanelLayer>();
        var byKey = new Dictionary<string, PanelLayer>();
        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            if (source == "none") continue;
            var key = source == "slide" ? source + i : source;
            if (!byKey.TryGetValue(key, out var layer))
            {
                var blocks = (source == "slide" ? slides[i] : hero).Value<BlockListModel>("panel")?.ToList() ?? [];
                layer = source switch
                {
                    "slide" or "hero" => FromBlocks(blocks),
                    "navigator" => new PanelLayer(false, [], navigator),
                    _ => new PanelLayer(source == "art", blocks.Where(b => b.Content.ContentType.Alias == "panelCards").ToList(), null),
                };
                byKey[key] = layer;
                if (!layer.IsEmpty) layers.Add(layer);
            }
            layer.Slides.Add(i);
        }
        return layers;
    }

    private static PanelLayer FromBlocks(List<BlockListItem> blocks) =>
        new(blocks.Any(b => b.Content.ContentType.Alias == "panelArt"), blocks.Where(b => b.Content.ContentType.Alias != "panelArt").ToList(), null);

    /// <summary>Slide transition (hero setting) → data-transition value (allow-list; default fade).</summary>
    public static string Transition(string? setting) => setting?.Trim() switch
    {
        "Slide" => "slide",
        "Rise" => "rise",
        "Zoom" => "zoom",
        "Blur" => "blur",
        _ => "fade",
    };

    /// <summary>The slide's "show" / "hide" wins; otherwise the hero's setting.</summary>
    public static bool ShowMark(bool heroHidesMark, string? slideMark) => slideMark switch
    {
        "show" => true,
        "hide" => false,
        _ => !heroHidesMark,
    };
}
