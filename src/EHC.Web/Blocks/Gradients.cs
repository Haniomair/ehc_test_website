using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EHC.Web.Themes;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace EHC.Web.Blocks;

/// <summary>A section background without a photo: a CSS class plus, for a picked gradient, its CSS.</summary>
public sealed record Background(string Class, string? Style);

/// <summary>A colour at a position (0–100 %). Colour = a theme colour label ("Deep 900") or "#RRGGBB".</summary>
public sealed record GradientStop(double At, string Colour);

/// <summary>A soft light at X/Y (0–100 % of the section) with a strength (0–100 %).</summary>
public sealed record GradientGlow(string Colour, double X, double Y, double Strength);

/// <summary>What the gradient editor (App_Plugins/EhcGradient) stores in a Gradient's "design" property.</summary>
public sealed record GradientDesign(int Angle, IReadOnlyList<GradientStop> Stops, GradientGlow? Glow);

/// <summary>
/// Gradients editors design under Site settings › Gradients and pick on heroes, slides and banners.
/// A colour is a theme colour ("Primary 500" → var(--brand-500), so occasion themes recolour it) or a custom hex.
/// Nothing stored reaches the page unchecked: labels map through an allow-list, hex must match #RRGGBB, numbers are
/// clamped, so the CSS built here is safe in a style attribute. Rendered by .g-bg in frontend/src/css/ehc.css.
/// </summary>
public static class Gradients
{
    public const int MaxStops = 8;

    /// <summary>Editor-facing family names → CSS role names.</summary>
    private static readonly Dictionary<string, string> Families = new(StringComparer.Ordinal)
    {
        ["Primary"] = "brand", ["Deep"] = "deep", ["Night"] = "night", ["Accent"] = "accent",
        ["Highlight"] = "highlight", ["Support"] = "support",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>"Primary 500" → "--brand-500" when that role variable exists; otherwise null.</summary>
    public static string? ThemeVar(string? label)
    {
        label = Single(label);
        if (string.IsNullOrWhiteSpace(label)) return null;
        var parts = label.Trim().Split(' ', 2);
        if (parts.Length != 2 || !Families.TryGetValue(parts[0], out var family)) return null;
        var name = $"--{family}-{parts[1]}";
        return Palette.Defaults.ContainsKey(name) ? name : null;
    }

    /// <summary>A colour's CSS value: "#RRGGBB" (upper case) or var(--role); null when it is neither.</summary>
    public static string? Colour(string? colour)
    {
        colour = colour?.Trim();
        if (string.IsNullOrEmpty(colour)) return null;
        if (ThemeResolver.HexColor().IsMatch(colour)) return colour.ToUpperInvariant();
        return ThemeVar(colour) is { } v ? $"var({v})" : null;
    }

    /// <summary>
    /// Reads and cleans a stored design: unknown colours dropped, numbers clamped, stops sorted, at most
    /// <see cref="MaxStops"/>. Null when fewer than two usable stops remain or the JSON is not a design.
    /// </summary>
    public static GradientDesign? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        GradientDesign? raw;
        try { raw = JsonSerializer.Deserialize<GradientDesign>(json, JsonOptions); }
        catch (JsonException) { return null; }
        catch (NotSupportedException) { return null; }
        if (raw?.Stops is null) return null;

        var stops = raw.Stops
            .Where(s => s is not null && Colour(s.Colour) is not null && double.IsFinite(s.At))
            .Select(s => s with { At = Math.Round(Math.Clamp(s.At, 0, 100), 1), Colour = s.Colour.Trim() })
            .OrderBy(s => s.At)
            .Take(MaxStops)
            .ToList();
        if (stops.Count < 2) return null;

        var glow = raw.Glow is { } g && Colour(g.Colour) is not null && double.IsFinite(g.X) && double.IsFinite(g.Y) && double.IsFinite(g.Strength)
            ? new GradientGlow(g.Colour.Trim(), Math.Round(Math.Clamp(g.X, 0, 100), 1), Math.Round(Math.Clamp(g.Y, 0, 100), 1), Math.Round(Math.Clamp(g.Strength, 0, 100)))
            : null;
        return new GradientDesign(((raw.Angle % 360) + 360) % 360, stops, glow);
    }

    public static string Json(GradientDesign design) => JsonSerializer.Serialize(design, JsonOptions);

    /// <summary>The CSS background value for a design.</summary>
    public static string Css(GradientDesign d)
    {
        static string N(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        var linear = $"linear-gradient({d.Angle}deg, " + string.Join(", ", d.Stops.Select(s => $"{Colour(s.Colour)} {N(s.At)}%")) + ")";
        if (d.Glow is not { } g) return linear;
        var glow = g.Strength >= 100 ? Colour(g.Colour) : $"color-mix(in srgb, {Colour(g.Colour)} {N(g.Strength)}%, transparent)";
        return $"radial-gradient(900px 600px at {N(g.X)}% {N(g.Y)}%, {glow} 0%, transparent 60%), {linear}";
    }

    public static string? Style(IPublishedContent gradient)
    {
        var design = Parse(Raw(gradient.Value("design")));
        return design is null ? null : $"--g-bg:{Css(design)}";
    }

    /// <summary>
    /// The background for a block: its picked gradient, else <paramref name="fallbackClass"/> (the theme's default
    /// gradient for that kind of section).
    /// </summary>
    public static Background For(IPublishedElement block, string fallbackClass = "hero-bg-0") =>
        block.Value<IPublishedContent>("gradient") is { } picked && Style(picked) is { } style ? new("g-bg", style) : new(fallbackClass, null);

    /// <summary>
    /// White-text contrast of every stop (the glow is a highlight and not checked), judged with the default EHC
    /// colours (occasion themes keep their own colours readable, see Palette).
    /// </summary>
    public static IReadOnlyList<(int Stop, double Ratio)> Readability(GradientDesign design) =>
        design.Stops.Select((s, i) => (i + 1, Math.Round(Contrast.Ratio("#FFFFFF", Hex(s.Colour)), 2))).ToList();

    /// <summary>A colour as #RRGGBB, theme colours at their EHC default.</summary>
    public static string Hex(string colour) => Colour(colour) is { } css && css.StartsWith("var(", StringComparison.Ordinal) ? Palette.Defaults[css[4..^1]] : Colour(colour)!;

    /// <summary>
    /// The stored JSON text of a published "design" value: Umbraco's JSON value converter returns a JsonDocument
    /// (whose ToString() is its type name, not the JSON); saving code sees the plain string.
    /// </summary>
    public static string? Raw(object? value) => value switch
    {
        null => null,
        string s => s,
        JsonDocument d => d.RootElement.GetRawText(),
        JsonElement e => e.GetRawText(),
        System.Text.Json.Nodes.JsonNode n => n.ToJsonString(),
        _ => value.ToString(),
    };

    /// <summary>Dropdown-style values may arrive as ["x"] (JSON).</summary>
    private static string? Single(string? raw)
    {
        if (raw is null || !raw.StartsWith('[')) return raw;
        try { return JsonSerializer.Deserialize<string[]>(raw)?.FirstOrDefault(); }
        catch (JsonException) { return null; }
    }
}
