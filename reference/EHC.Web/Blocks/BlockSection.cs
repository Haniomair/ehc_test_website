// REFERENCE CODE — port, build, fix. Reads the shared `blockSettings` element (docs/03-blocks-catalog.md).
using System.Text.RegularExpressions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;
using EHC.Web.Themes;

namespace EHC.Web.Blocks;

/// <summary>Wrapper decisions for one block: visible? which classes / id / theme?</summary>
public sealed partial record BlockSection(bool Visible, string? AnchorId, string? ThemeOverride, string CssClass)
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

    [GeneratedRegex("^[a-z0-9-]{1,60}$")]
    private static partial Regex SafeId();

    public static BlockSection From(IPublishedElement? settings, IPublishedValueFallback fallback, DateTime nowUtc)
    {
        if (settings is null) return new(true, null, null, Spacing["md"]);

        if (settings.Value<bool>(fallback, "hide")) return new(false, null, null, "");

        var from = ThemeResolver.ToUtc(settings.Value(fallback, "showFrom"));
        var until = ThemeResolver.ToUtc(settings.Value(fallback, "showUntil"));
        if (from is not null && nowUtc < from) return new(false, null, null, "");
        if (until is not null && nowUtc >= until) return new(false, null, null, "");

        var variant = settings.Value<string>(fallback, "variant") ?? "default";
        var spacing = settings.Value<string>(fallback, "spacing") ?? "md";
        var anchor = settings.Value<string>(fallback, "anchorId");
        var theme = settings.Value<string>(fallback, "themeOverride");

        var css = string.Join(' ',
            Variants.GetValueOrDefault(variant, ""),
            Spacing.GetValueOrDefault(spacing, Spacing["md"])).Trim();

        return new(
            true,
            anchor is not null && SafeId().IsMatch(anchor) ? anchor : null,
            theme is not null && theme != "default" && SafeId().IsMatch(theme) ? theme : null,
            css);
    }
}
