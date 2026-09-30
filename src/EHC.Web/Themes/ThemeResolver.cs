using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace EHC.Web.Themes;

/// <summary>The theme that applies to the current request (or to one section).</summary>
public sealed record ResolvedTheme(
    IReadOnlyDictionary<string, string> CssVariables,
    string? LogoEnUrl,
    string? LogoArUrl,
    string? PatternCss,
    string? RibbonText,
    string? RibbonUrl)
{
    public static readonly ResolvedTheme Default = new(new Dictionary<string, string>(), null, null, null, null, null);

    /// <summary>Value for a style attribute (on &lt;html&gt; or a section). Only generated hex colours and a checked motif URL.</summary>
    public string? InlineStyle
    {
        get
        {
            var parts = CssVariables.Select(kv => $"{kv.Key}:{kv.Value}").ToList();
            if (PatternCss is not null) parts.Add($"--pattern-image:{PatternCss}");
            // only the EHC star turns; pictures (Kaaba, ram, crescent, uploads) stay upright
            if (PatternCss is not null && PatternCss != ThemeResolver.StarPattern) parts.Add("--pattern-spin:none");
            return parts.Count == 0 ? null : string.Join(";", parts);
        }
    }
}

public interface IThemeResolver
{
    ResolvedTheme Resolve(IPublishedContent current);
}

/// <summary>
/// Picks the theme for a request: a backoffice user's preview (ThemePreview), else a reviewer's look-customizer choice,
/// else the first live entry of the schedule
/// in Site settings, else the default theme. A theme's look is built from its colours by <see cref="Palette"/>
/// (docs/04-theme-system.md); nothing comes from CSS presets.
/// </summary>
public sealed partial class ThemeResolver(
    IPublishedValueFallback fallback,
    TimeProvider clock,
    ILogger<ThemeResolver> logger,
    IHttpContextAccessor http,
    IUmbracoContextAccessor umbraco) : IThemeResolver
{
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    internal static partial Regex HexColor();

    [GeneratedRegex("^[a-z0-9-]{1,40}$")]
    internal static partial Regex SafeKey();

    // media URL placed inside url('…'): no quotes, brackets, backslashes or whitespace
    [GeneratedRegex(@"^/[^'""()\\\s<>]{1,300}$")]
    private static partial Regex SafeUrl();

    /// <summary>Built-in motifs (the "EHC - Theme motif" dropdown); "EHC star" is the default pattern from _tokens.css.</summary>
    private static readonly Dictionary<string, string> Motifs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ramadan"] = "url('/assets/img/motifs/ramadan.svg')",
        ["Eid al-Fitr"] = "url('/assets/img/motifs/eid-fitr.svg')",
        ["Eid al-Adha"] = "url('/assets/img/motifs/eid-adha.svg')",
        ["Eid"] = "url('/assets/img/motifs/eid-fitr.svg')",   // value used before the two Eids had their own motif
        ["Hajj"] = "url('/assets/img/motifs/hajj.svg')",
        ["None"] = "none",
    };

    /// <summary>The default pattern (EHC star), as the customizer sets it explicitly.</summary>
    public const string StarPattern = "url('/assets/img/mark.png')";

    /// <summary>Motifs a reviewer can try in the look customizer (key → CSS), "star" being the EHC mark.</summary>
    public static readonly IReadOnlyDictionary<string, string> CustomizerPatterns = new Dictionary<string, string>
    {
        ["star"] = StarPattern,
        ["ramadan"] = Motifs["Ramadan"],
        ["eid-fitr"] = Motifs["Eid al-Fitr"],
        ["eid-adha"] = Motifs["Eid al-Adha"],
        ["hajj"] = Motifs["Hajj"],
        ["none"] = "none",
    };

    // themes change rarely: cache the built look per theme version and culture (ribbon text is per language)
    private static readonly ConcurrentDictionary<(Guid, DateTime, string), ResolvedTheme> Cache = new();

    public ResolvedTheme Resolve(IPublishedContent current)
    {
        var resolved = ResolveTheme(current);
        // a reviewer's pattern choice in the look customizer replaces the theme's motif (allow-listed key)
        return EHC.Web.Site.Customizer.Current(http.HttpContext)?.Pattern is { } key && CustomizerPatterns.TryGetValue(key, out var css)
            ? resolved with { PatternCss = css }
            : resolved;
    }

    private ResolvedTheme ResolveTheme(IPublishedContent current)
    {
        if (PreviewTheme() is { } preview) return Build(preview, fallback, logger);
        if (CustomizerTheme() is { } chosen) return Build(chosen, fallback, logger);

        var home = current.AncestorOrSelf("home") ?? current.Root();
        var settings = home?.Value<IPublishedContent>(fallback, "settings");
        if (settings is null) return ResolvedTheme.Default;

        var theme = FindScheduledTheme(settings, clock.GetUtcNow().UtcDateTime)
                    ?? settings.Value<IPublishedContent>(fallback, "defaultTheme");

        return theme is null ? ResolvedTheme.Default : Build(theme, fallback, logger);
    }

    /// <summary>The theme a signed-in backoffice user asked to preview (?previewTheme=…, checked by ThemePreview).</summary>
    private IPublishedContent? PreviewTheme()
    {
        if (http.HttpContext?.Items[ThemePreview.ItemKey] is not Guid key) return null;
        if (!umbraco.TryGetUmbracoContext(out var ctx) || ctx.Content is null) return null;
        var node = ctx.Content.GetById(true, key) ?? ctx.Content.GetById(key);
        return node?.ContentType.Alias == "theme" ? node : null;
    }

    /// <summary>The published theme a reviewer picked in the look customizer (Site/Customizer.cs).</summary>
    private IPublishedContent? CustomizerTheme()
    {
        if (EHC.Web.Site.Customizer.Current(http.HttpContext)?.Theme is not Guid key) return null;
        if (!umbraco.TryGetUmbracoContext(out var ctx) || ctx.Content is null) return null;
        var node = ctx.Content.GetById(key);
        return node?.ContentType.Alias == "theme" ? node : null;
    }

    private IPublishedContent? FindScheduledTheme(IPublishedContent settings, DateTime nowUtc)
    {
        var schedule = settings.Value<BlockListModel>(fallback, "themeSchedule");
        if (schedule is null) return null;

        foreach (var item in schedule)
        {
            var entry = item.Content;
            if (!entry.Value<bool>(fallback, "enabled")) continue;

            var start = ToUtc(entry.Value(fallback, "startDate"));
            var end = ToUtc(entry.Value(fallback, "endDate"));
            if (start is null || end is null) continue;

            if (IsLive(start.Value, end.Value, nowUtc))
                return entry.Value<IPublishedContent>(fallback, "theme"); // first live entry wins
        }
        return null;
    }

    /// <summary>Start inclusive, end exclusive. All values UTC.</summary>
    internal static bool IsLive(DateTime startUtc, DateTime endUtc, DateTime nowUtc)
        => startUtc <= nowUtc && nowUtc < endUtc;

    public static DateTime? ToUtc(object? value) => value switch
    {
        DateTimeOffset dto => dto.UtcDateTime,
        DateTime dt when dt == default => null,
        DateTime dt => dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime(),
        string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var p) => p.UtcDateTime,
        _ => null,
    };

    /// <summary>A Theme node's colours, motif, logos and ribbon — for the whole page or a single section.</summary>
    public static ResolvedTheme Build(IPublishedContent theme, IPublishedValueFallback fallback, ILogger? logger = null)
    {
        var cacheKey = (theme.Key, theme.UpdateDate, CultureInfo.CurrentUICulture.Name);
        if (Cache.TryGetValue(cacheKey, out var cached)) return cached;

        string? V(string alias) => theme.Value<string>(fallback, alias);
        var palette = Palette.Build(
            new ThemeColors(V("primaryColor"), V("darkColor"), V("accentColor"), V("highlightColor"), V("supportColor")),
            Palette.FineTune.ToDictionary(f => f.Alias, f => V(f.Alias)));
        foreach (var problem in palette.Problems)
            logger?.LogWarning("Theme {Theme}: {Problem}", theme.Name, problem);

        var custom = theme.Value<IPublishedContent>(fallback, "patternImage")?.Url();
        var pattern = custom is not null && SafeUrl().IsMatch(custom) ? $"url('{custom}')"
                    : V("motif") is { } motif && Motifs.TryGetValue(motif, out var css) ? css
                    : null;

        var resolved = new ResolvedTheme(
            palette.Variables,
            theme.Value<IPublishedContent>(fallback, "logoEn")?.Url(),
            theme.Value<IPublishedContent>(fallback, "logoAr")?.Url(),
            pattern,
            theme.Value<string>(fallback, "ribbonText"),
            theme.Value<Umbraco.Cms.Core.Models.Link>(fallback, "ribbonLink")?.Url);

        if (Cache.Count > 500) Cache.Clear();
        Cache[cacheKey] = resolved;
        return resolved;
    }
}
