using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using EHC.Web.Themes;

namespace EHC.Web.Site;

/// <summary>
/// Look customizer for reviewers of the showcase (Ehc:Customizer:Enabled). Choices live in one cookie in the reviewer's
/// browser and change only what that browser is served: theme, hero options, section order/visibility and a few
/// global look classes. Nothing is saved to Umbraco. Every value is checked against an allow-list; anything else is
/// ignored. Pages rendered with choices are never cached.
/// </summary>
public static partial class Customizer
{
    public const string CookieName = "ehc-customize";
    /// <summary>Set by customizer.js for a few seconds before it reloads: render the panel open (no slide-in).</summary>
    public const string OpenCookieName = "ehc-customize-open";
    internal const string OnKey = "ehc:customizer";
    internal const string StateKey = "ehc:customizer-state";
    private const int MaxCookieLength = 2048;
    private const int MaxKeys = 80;

    public static readonly IReadOnlyList<string> HeroPanels = ["art", "navigator", "cards", "none"];
    public static readonly IReadOnlyList<string> Sides = ["start", "end"];
    public static readonly IReadOnlyList<string> Frames = ["tilted", "plain"];
    public static readonly IReadOnlyList<string> Aligns = ["start", "center"];
    public static IReadOnlyCollection<string> Patterns => ThemeResolver.CustomizerPatterns.Keys.ToList();

    /// <summary>Global look options: value → class on &lt;html&gt; (styles in ehc.css, "look customizer"). First value = default.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Looks = new Dictionary<string, string[]>
    {
        ["corners"] = ["soft", "square", "round"],
        ["shadows"] = ["soft", "flat", "deep"],
        ["density"] = ["normal", "compact", "airy"],
        ["buttons"] = ["pill", "rounded"],
    };

    [GeneratedRegex("^[0-9a-f]{8}$")]
    private static partial Regex BlockKey();

    public static void Add(IUmbracoBuilder builder)
        => builder.Services.Configure<UmbracoPipelineOptions>(o => o.AddFilter(new UmbracoPipelineFilter("EhcCustomizer")
        {
            PostRouting = app =>
            {
                var enabled = app.ApplicationServices.GetRequiredService<IConfiguration>().GetValue<bool>("Ehc:Customizer:Enabled");
                if (!enabled) return;
                app.Use(async (context, next) =>
                {
                    if (!SecurityHeaders.IsBackoffice(context.Request.Path))
                    {
                        context.Items[OnKey] = true;
                        if (Parse(context.Request.Cookies[CookieName]) is { } state)
                        {
                            context.Items[StateKey] = state;
                            context.Response.OnStarting(() =>
                            {
                                context.Response.Headers.CacheControl = "no-store, private";
                                return Task.CompletedTask;
                            });
                        }
                    }
                    await next(context);
                });
            },
        }));

    /// <summary>True when the customizer is switched on for this site (the panel is shown).</summary>
    public static bool IsOn(HttpContext? context) => context?.Items[OnKey] is true;

    /// <summary>The reviewer's choices, or null when the customizer is off or nothing was chosen.</summary>
    public static CustomizerState? Current(HttpContext? context) => context?.Items[StateKey] as CustomizerState;

    /// <summary>Reads the cookie (query-string format, written by components/customizer.js).</summary>
    internal static CustomizerState? Parse(string? cookie)
    {
        if (string.IsNullOrWhiteSpace(cookie) || cookie.Length > MaxCookieLength) return null;
        var q = QueryHelpers.ParseQuery(cookie.StartsWith('?') ? cookie : "?" + cookie);
        string? Pick(string key, IReadOnlyList<string> allowed)
            => q.TryGetValue(key, out var v) && allowed.Contains(v.ToString()) ? v.ToString() : null;
        IReadOnlyList<string> Keys(string key)
            => q.TryGetValue(key, out var v)
                ? v.ToString().Split('.', StringSplitOptions.RemoveEmptyEntries).Where(k => BlockKey().IsMatch(k)).Distinct().Take(MaxKeys).ToList()
                : [];

        var looks = new Dictionary<string, string>();
        foreach (var (name, values) in Looks)
            if (Pick(name, values) is { } v && v != values[0]) looks[name] = v;

        var state = new CustomizerState
        {
            Theme = q.TryGetValue("theme", out var t) && Guid.TryParse(t, out var g) ? g : null,
            HeroPanel = Pick("panel", HeroPanels),
            HeroRotate = q.TryGetValue("rotate", out var r) ? r == "1" ? true : r == "0" ? false : null : null,
            HeroSide = Pick("side", Sides),
            HeroFrame = Pick("frame", Frames),
            HeroAlign = Pick("align", Aligns),
            Pattern = Pick("pattern", Patterns.ToList()),
            Looks = looks,
            Hidden = Keys("hide"),
            Order = Keys("order"),
        };
        return state.IsEmpty ? null : state;
    }

    /// <summary>Three colours of a theme for its swatch in the panel (validated hex, EHC defaults where the theme has none).</summary>
    public static string SwatchStyle(ResolvedTheme theme)
    {
        string C(string v) => theme.CssVariables.GetValueOrDefault(v) is { } c && ThemeResolver.HexColor().IsMatch(c) ? c : Palette.Defaults[v];
        return $"--sw-a:{C("--brand-600")};--sw-b:{C("--deep-900")};--sw-c:{C("--accent-400")}";
    }

    /// <summary>First 8 hex characters of a block's content key: how the cookie refers to a section.</summary>
    public static string ShortKey(Guid contentKey) => contentKey.ToString("N")[..8];
}

public sealed record CustomizerState
{
    public Guid? Theme { get; init; }
    public string? HeroPanel { get; init; }
    public bool? HeroRotate { get; init; }
    public string? HeroSide { get; init; }
    public string? HeroFrame { get; init; }
    public string? HeroAlign { get; init; }
    public string? Pattern { get; init; }
    public IReadOnlyDictionary<string, string> Looks { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> Hidden { get; init; } = [];
    public IReadOnlyList<string> Order { get; init; } = [];

    public bool IsEmpty => Theme is null && HeroPanel is null && HeroRotate is null && HeroSide is null && HeroFrame is null
                           && HeroAlign is null && Pattern is null && Looks.Count == 0 && Hidden.Count == 0 && Order.Count == 0;

    /// <summary>Classes for &lt;html&gt;, e.g. "look-corners-square look-density-compact" (all from the allow-list).</summary>
    public string? HtmlClass => Looks.Count == 0 ? null : string.Join(" ", Looks.Select(kv => $"look-{kv.Key}-{kv.Value}"));

    public bool IsHidden(Guid contentKey) => Hidden.Contains(Customizer.ShortKey(contentKey));

    /// <summary>Blocks in the reviewer's order: listed sections by their position in the list, the rest keep their place.</summary>
    public IReadOnlyList<T> Arrange<T>(IReadOnlyList<T> items, Func<T, Guid> key)
    {
        if (Order.Count == 0) return items;
        var rank = Order.Select((k, i) => (k, i)).ToDictionary(x => x.k, x => x.i);
        var listed = items.Where(x => rank.ContainsKey(Customizer.ShortKey(key(x)))).OrderBy(x => rank[Customizer.ShortKey(key(x))]).ToList();
        var slots = new Queue<T>(listed);
        return items.Select(x => rank.ContainsKey(Customizer.ShortKey(key(x))) ? slots.Dequeue() : x).ToList();
    }
}
