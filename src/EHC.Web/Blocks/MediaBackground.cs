using System.Globalization;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Extensions;

namespace EHC.Web.Blocks;

/// <summary>
/// Reads mediaComposition (backgroundImage, backgroundVideo, poster, overlay, overlayStrength, showPattern, imageAlt)
/// into validated values for the media-hero markup: &lt;section class="media-hero" data-overlay data-strength style="--focal"&gt;.
/// </summary>
public sealed record MediaBackground(
    string? ImageDesktop,
    string? ImageMobile,
    string? FrameImage,
    string Alt,
    string? VideoUrl,
    string? VideoType,
    string? PosterUrl,
    string Overlay,
    string Strength,
    string Focal,
    bool ShowPattern,
    bool Lazy = false)
{
    public bool HasImage => ImageDesktop is not null;
    public bool HasVideo => VideoUrl is not null;
    public bool HasMedia => HasImage || HasVideo;

    /// <summary>Value for style="…" on the media element (validated numbers only).</summary>
    public string FocalStyle => $"--focal: {Focal}";

    private static readonly HashSet<string> Overlays = ["tint", "duotone", "shade"];
    private static readonly HashSet<string> Strengths = ["light", "medium", "strong"];

    public static MediaBackground From(IPublishedElement e)
    {
        var image = e.Value<MediaWithCrops>("backgroundImage");
        var video = e.Value<MediaWithCrops>("backgroundVideo");
        var poster = e.Value<MediaWithCrops>("poster");

        var overlay = e.Value<string>("overlay");
        var strength = e.Value<string>("overlayStrength");
        if (overlay is null || !Overlays.Contains(overlay))
        {
            // "none" (or empty) would leave text directly on a photo: fall back to a readable shade (docs/03)
            overlay = "shade";
            if (strength is null or "light") strength = "medium";
        }
        if (strength is null || !Strengths.Contains(strength)) strength = "medium";

        string? videoUrl = video?.Url();
        var ext = Path.GetExtension(videoUrl ?? "").ToLowerInvariant();
        var videoType = ext switch { ".webm" => "video/webm", ".mp4" or ".m4v" => "video/mp4", _ => null };
        if (videoType is null) videoUrl = null;

        return new MediaBackground(
            Crop(image, "desktop"),
            Crop(image, "mobile"),
            image?.GetCropUrl(1200, 1020, furtherOptions: Webp) ?? image?.Url(),
            e.Value<string>("imageAlt")?.Trim() ?? "",
            videoUrl,
            videoType,
            poster?.GetCropUrl(1920, 1080, furtherOptions: Webp) ?? poster?.Url(),
            overlay,
            strength,
            FocalOf(image),
            e.Value<bool>("showPattern"));
    }

    /// <summary>Inner-page header image (pageHeaderComposition): theme tint, medium, decorative (the h1 carries the meaning).</summary>
    public static MediaBackground ForHeader(MediaWithCrops? image) => new(
        Crop(image, "desktop"),
        Crop(image, "mobile"),
        image?.Url(),
        "",
        null,
        null,
        null,
        "tint",
        "medium",
        FocalOf(image),
        false);

    private const string Webp = "format=webp&quality=80";

    private static string? Crop(MediaWithCrops? image, string alias) =>
        image is null ? null : image.GetCropUrl(cropAlias: alias, useCropDimensions: true, furtherOptions: Webp) ?? image.Url();

    private static string FocalOf(MediaWithCrops? image)
    {
        var fp = image?.LocalCrops?.FocalPoint ?? image?.Content.Value<ImageCropperValue>("umbracoFile")?.FocalPoint;
        var left = fp?.Left ?? 0.5m;
        var top = fp?.Top ?? 0.5m;
        return string.Create(CultureInfo.InvariantCulture, $"{Math.Clamp(left, 0, 1) * 100:0.#}% {Math.Clamp(top, 0, 1) * 100:0.#}%");
    }
}
