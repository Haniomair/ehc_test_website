using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace EHC.Web.Themes;

/// <summary>The theme that applies to the current request.</summary>
public sealed record ResolvedTheme(
    string? PresetKey,
    IReadOnlyDictionary<string, string> CssVariables,
    string? LogoEnUrl,
    string? LogoArUrl,
    string? PatternUrl,
    string? RibbonText,
    string? RibbonUrl)
{
    public static readonly ResolvedTheme Default =
        new(null, new Dictionary<string, string>(), null, null, null, null, null);

    /// <summary>Value for the style attribute on &lt;html&gt;. Only contains validated tokens.</summary>
    public string? InlineStyle
    {
        get
        {
            var parts = CssVariables.Select(kv => $"{kv.Key}:{kv.Value}").ToList();
            if (PatternUrl is not null) parts.Add($"--pattern-image:url('{PatternUrl}')");
            return parts.Count == 0 ? null : string.Join(";", parts);
        }
    }
}

public interface IThemeResolver
{
    ResolvedTheme Resolve(IPublishedContent current);
}

public sealed partial class ThemeResolver(
    IPublishedValueFallback fallback,
    TimeProvider clock,
    ILogger<ThemeResolver> logger) : IThemeResolver
{
    // Editor-supplied colour → CSS variable. Keep this list short on purpose (see docs/04-theme-system.md).
    private static readonly (string Alias, string CssVar)[] ColorOverrides =
    [
        ("brand500", "--brand-500"),
        ("brand600", "--brand-600"),
        ("deep900", "--deep-900"),
        ("accent400", "--accent-400"),
    ];

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    internal static partial Regex HexColor();

    [GeneratedRegex("^[a-z0-9-]{1,40}$")]
    internal static partial Regex SafeKey();

    public ResolvedTheme Resolve(IPublishedContent current)
    {
        var home = current.Root();
        var settings = home?.Value<IPublishedContent>(fallback, "settings");
        if (settings is null) return ResolvedTheme.Default;

        var theme = FindScheduledTheme(settings, clock.GetUtcNow().UtcDateTime)
                    ?? settings.Value<IPublishedContent>(fallback, "defaultTheme");

        return theme is null ? ResolvedTheme.Default : Build(theme);
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

    internal static DateTime? ToUtc(object? value) => value switch
    {
        DateTimeOffset dto => dto.UtcDateTime,
        DateTime dt when dt == default => null,
        DateTime dt => dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime(),
        string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var p) => p.UtcDateTime,
        _ => null,
    };

    private ResolvedTheme Build(IPublishedContent theme)
    {
        var preset = theme.Value<string>(fallback, "presetKey");
        if (string.IsNullOrWhiteSpace(preset) || preset == "default" || !SafeKey().IsMatch(preset)) preset = null;

        var vars = new Dictionary<string, string>();
        foreach (var (alias, cssVar) in ColorOverrides)
        {
            var value = theme.Value<string>(fallback, alias)?.Trim();
            if (string.IsNullOrEmpty(value)) continue;
            if (HexColor().IsMatch(value)) vars[cssVar] = value;
            else logger.LogWarning("Theme {Theme}: ignored invalid colour {Alias}={Value}", theme.Name, alias, value);
        }

        return new ResolvedTheme(
            preset,
            vars,
            theme.Value<IPublishedContent>(fallback, "logoEn")?.Url(),
            theme.Value<IPublishedContent>(fallback, "logoAr")?.Url(),
            theme.Value<IPublishedContent>(fallback, "patternImage")?.Url(),
            theme.Value<string>(fallback, "ribbonText"),
            theme.Value<Umbraco.Cms.Core.Models.Link>(fallback, "ribbonLink")?.Url);
    }
}
