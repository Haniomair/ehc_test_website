// Reads the shared `blockSettings` element (docs/03-blocks-catalog.md).
using System.Text.RegularExpressions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;
using EHC.Web.Themes;

namespace EHC.Web.Blocks;

/// <summary>Wrapper decisions for one block: visible? which classes / id? ThemeStyle = CSS variables of a section theme.</summary>
public sealed partial record BlockSection(bool Visible, string? AnchorId, string? ThemeStyle, string CssClass)
{
    // Allow-list: editors choose a key, never a class name. Keep in sync with frontend/src/css/_safelist.css.
    private static readonly Dictionary<string, string> Variants = new()
    {
        ["default"] = "",
        ["soft"] = "bg-soft dark:bg-night-900/60",
        ["deep"] = "bg-deep-900 text-white dark:bg-deep-950",
        ["brand"] = "bg-brand-500 text-white",
    };

    private static readonly Dictionary<string, string> Spacing = new()
    {
        ["none"] = "",
        ["sm"] = "py-10",
        ["md"] = "py-16 lg:py-24",
        ["lg"] = "py-24 lg:py-32",
    };

    private const string PlainMarker = "sec-plain";

    [GeneratedRegex("^[a-z0-9-]{1,60}$")]
    private static partial Regex SafeId();

    public static BlockSection From(IPublishedElement? settings, IPublishedValueFallback fallback, DateTime nowUtc)
    {
        if (settings is null) return new(true, null, null, $"{Spacing["md"]} {PlainMarker}");

        if (settings.Value<bool>(fallback, "hide")) return new(false, null, null, "");

        var from = ThemeResolver.ToUtc(settings.Value(fallback, "showFrom"));
        var until = ThemeResolver.ToUtc(settings.Value(fallback, "showUntil"));
        if (from is not null && nowUtc < from) return new(false, null, null, "");
        if (until is not null && nowUtc >= until) return new(false, null, null, "");

        var variant = settings.Value<string>(fallback, "variant") ?? "default";
        var spacing = settings.Value<string>(fallback, "spacing") ?? "md";
        var anchor = settings.Value<string>(fallback, "anchorId");
        var theme = settings.Value<IPublishedContent>(fallback, "themeOverride");

        var pad = Spacing.GetValueOrDefault(spacing, Spacing["md"]);
        var bg = Variants.GetValueOrDefault(variant, "");
        // padded sections on the page background are marked so two in a row share one gap (see ehc.css)
        var css = string.Join(' ', bg, pad, bg.Length == 0 && pad.Length > 0 ? PlainMarker : "").Trim();

        return new(
            true,
            anchor is not null && SafeId().IsMatch(anchor) ? anchor : null,
            theme?.ContentType.Alias == "theme" ? ThemeResolver.Build(theme, fallback).InlineStyle : null,
            css);
    }
}
