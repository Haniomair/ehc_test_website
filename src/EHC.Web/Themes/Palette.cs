using System.Globalization;

namespace EHC.Web.Themes;

/// <summary>Base colours an editor picks on a Theme node. Null or empty = keep the site default for that colour family.</summary>
public sealed record ThemeColors(string? Primary, string? Dark, string? Accent, string? Highlight, string? Support);

/// <summary>
/// Builds a theme's full set of CSS role variables from a few base colours (docs/04-theme-system.md):
/// shades are mixed in OKLab, readability is enforced (WCAG AA, the same pairs as frontend/scripts/check-contrast.mjs),
/// and optional fine-tune values replace single shades. Only variables the theme changes are returned; everything
/// else keeps the default from _tokens.css.
/// </summary>
public static class Palette
{
    /// <summary>EHC defaults — must equal :root in frontend/src/css/_tokens.css (a test compares them).</summary>
    public static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        ["--brand-50"] = "#EAF5FC", ["--brand-100"] = "#D3EAF7", ["--brand-200"] = "#A9D5EF", ["--brand-300"] = "#7FD3FF",
        ["--brand-400"] = "#2FA8DF", ["--brand-500"] = "#2490CC", ["--brand-600"] = "#1C7DB4", ["--brand-700"] = "#17669A",
        ["--deep-700"] = "#123F73", ["--deep-800"] = "#0F3A6B", ["--deep-900"] = "#0A2A4F", ["--deep-950"] = "#061A33",
        ["--night-900"] = "#0B1B31", ["--night-950"] = "#060F1E",
        ["--accent-400"] = "#F2B544", ["--accent-500"] = "#E3A12C",
        ["--highlight-500"] = "#E0567B", ["--highlight-800"] = "#7A1F4F",
        ["--support-50"] = "#E6F5F3", ["--support-300"] = "#9BF0E2", ["--support-400"] = "#3BB6A9", ["--support-500"] = "#0E8F87",
        ["--support-800"] = "#0B4E6B", ["--support-900"] = "#06324A",
        ["--soft"] = "#F4F8FC", ["--on-deep"] = "#BFD6EE",
    };

    /// <summary>Fine-tune properties on the Theme node (alias → CSS variable). The allow-list: nothing else can be set.</summary>
    public static readonly (string Alias, string Var)[] FineTune =
    [
        ("brand50", "--brand-50"), ("brand100", "--brand-100"), ("brand200", "--brand-200"), ("brand300", "--brand-300"),
        ("brand400", "--brand-400"), ("brand500", "--brand-500"), ("brand600", "--brand-600"), ("brand700", "--brand-700"),
        ("deep700", "--deep-700"), ("deep800", "--deep-800"), ("deep900", "--deep-900"), ("deep950", "--deep-950"),
        ("night900", "--night-900"), ("night950", "--night-950"),
        ("accent400", "--accent-400"), ("accent500", "--accent-500"),
        ("highlight500", "--highlight-500"), ("highlight800", "--highlight-800"),
        ("support50", "--support-50"), ("support300", "--support-300"), ("support400", "--support-400"),
        ("support500", "--support-500"), ("support800", "--support-800"), ("support900", "--support-900"),
        ("soft", "--soft"), ("onDeep", "--on-deep"), ("heroGlow", "--hero-0-glow"),
    ];

    private const string White = "#FFFFFF";
    private const string DarkOnAccent = "#2B1D00"; // text colour of the sand button (.btn-sand)

    /// <summary>Readability rules (text, background, minimum ratio). Same pairs as check-contrast.mjs.</summary>
    public static readonly (string Label, string Text, string Background, double Min)[] Rules =
    [
        ("Deep 900 headings on the light section background", "--deep-900", "--soft", 7.0),
        ("white text on Primary 600 (buttons)", White, "--brand-600", 4.5),
        ("Primary 700 links on the light section background", "--brand-700", "--soft", 4.5),
        ("Primary 300 highlights on Deep 900", "--brand-300", "--deep-900", 4.5),
        ("muted text (On deep) on Deep 900", "--on-deep", "--deep-900", 4.5),
        ("dark text on Accent 400 (sand buttons)", DarkOnAccent, "--accent-400", 4.5),
    ];

    public sealed record Result(IReadOnlyDictionary<string, string> Variables, IReadOnlyList<string> Adjustments, IReadOnlyList<string> Problems);

    /// <param name="colors">base colours (invalid values are ignored)</param>
    /// <param name="fineTune">fine-tune alias → hex (invalid values are ignored)</param>
    public static Result Build(ThemeColors colors, IReadOnlyDictionary<string, string?> fineTune)
    {
        var vars = new Dictionary<string, string>();          // what the theme changes
        var adjustments = new List<string>();
        var fixedByEditor = new HashSet<string>();             // fine-tuned: never adjusted silently

        var primary = Valid(colors.Primary);
        var dark = Valid(colors.Dark);
        var accent = Valid(colors.Accent);
        var highlight = Valid(colors.Highlight);
        var support = Valid(colors.Support);
        var tuned = FineTune
            .Select(f => (f.Var, Hex: Valid(fineTune.TryGetValue(f.Alias, out var v) ? v : null)))
            .Where(f => f.Hex is not null)
            .ToDictionary(f => f.Var, f => f.Hex!);

        if (primary is not null)
        {
            // steps measured from the designed presets (tint: share of the way to white, chroma kept)
            var p = Oklab.FromHex(primary);
            vars["--brand-50"] = p.Tint(.919, .13).ToHex();
            vars["--brand-100"] = p.Tint(.821, .28).ToHex();
            vars["--brand-200"] = p.Tint(.644, .56).ToHex();
            vars["--brand-300"] = p.Tint(.640, .67).ToHex();
            vars["--brand-400"] = p.Tint(.203, 1.05).ToHex();
            vars["--brand-500"] = primary;
            vars["--brand-600"] = p.Scale(.897, .91).ToHex();
            vars["--brand-700"] = p.Scale(.780, .79).ToHex();
            vars["--soft"] = p.Tint(.947, .074).ToHex();
            vars["--on-deep"] = p.Tint(.728, .35).ToHex();
        }

        // the dark family follows the picked dark colour, or a deep version of the primary
        var deep = dark ?? (primary is null ? null : Oklab.FromHex(primary).WithLightness(.25).Desaturate(.42).ToHex());
        if (deep is not null)
        {
            // headings: make the dark colour dark enough before its family is derived from it
            var soft = tuned.GetValueOrDefault("--soft") ?? vars.GetValueOrDefault("--soft") ?? Defaults["--soft"];
            var fixedDeep = Darken(deep, soft, 7.0);
            if (dark is not null && !Same(fixedDeep, dark))
                adjustments.Add($"Dark colour darkened from {dark} to {fixedDeep} so headings stay readable.");
            var d = Oklab.FromHex(fixedDeep);
            vars["--deep-900"] = fixedDeep;
            vars["--deep-950"] = d.Scale(.777, .73).ToHex();
            vars["--deep-800"] = d.Scale(1.216, 1.3).ToHex();
            vars["--deep-700"] = d.Scale(1.402, 1.5).ToHex();
            vars["--night-900"] = d.Scale(.792, .61).ToHex();
            vars["--night-950"] = d.Scale(.619, .41).ToHex();
        }

        if (accent is not null)
        {
            var fixedAccent = Lighten(accent, DarkOnAccent, 4.5);
            if (!Same(fixedAccent, accent))
                adjustments.Add($"Accent colour lightened from {accent} to {fixedAccent} so button text stays readable.");
            vars["--accent-400"] = fixedAccent;
            vars["--accent-500"] = Oklab.FromHex(fixedAccent).Scale(.93, .98).ToHex();
        }

        if (highlight is not null)
        {
            var h = Oklab.FromHex(highlight);
            vars["--highlight-500"] = highlight;
            vars["--highlight-800"] = h.Scale(.6, .75).ToHex();
        }

        if (support is not null)
        {
            var s = Oklab.FromHex(support);
            vars["--support-50"] = s.Tint(.9, .15).ToHex();
            vars["--support-300"] = s.Tint(.5, .7).ToHex();
            vars["--support-400"] = s.Tint(.15, 1).ToHex();
            vars["--support-500"] = support;
            vars["--support-800"] = s.Scale(.6, .8).ToHex();
            vars["--support-900"] = s.Scale(.45, .7).ToHex();
        }

        foreach (var (v, hex) in tuned) { vars[v] = hex; fixedByEditor.Add(v); }

        // generated shades that miss a rule are nudged until they pass; fine-tuned ones are reported instead
        string Get(string v) => v.StartsWith('#') ? v : vars.GetValueOrDefault(v) ?? Defaults[v];
        var problems = new List<string>();
        foreach (var (label, text, background, min) in Rules)
        {
            if (!vars.ContainsKey(text) && !vars.ContainsKey(background)) continue; // untouched defaults pass
            var ratio = Contrast.Ratio(Get(text), Get(background));
            if (ratio >= min) continue;

            // adjust the side the theme generated (text for highlights, background for buttons / headings)
            var target = vars.ContainsKey(text) && !fixedByEditor.Contains(text) && !text.StartsWith('#') ? text
                       : vars.ContainsKey(background) && !fixedByEditor.Contains(background) && !background.StartsWith('#') ? background
                       : null;
            if (target is null)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{label}: {ratio:0.00}:1, needs at least {min:0.0}:1."));
                continue;
            }
            var other = target == text ? Get(background) : Get(text);
            vars[target] = Lighter(vars[target], other) ? Lighten(vars[target], other, min) : Darken(vars[target], other, min);
        }

        // hero gradients: set explicitly so a theme also works on a single section
        if (vars.Keys.Any(k => k.StartsWith("--brand-") || k.StartsWith("--deep-")) || vars.ContainsKey("--hero-0-glow"))
        {
            vars.TryAdd("--hero-0-glow", Get("--brand-400"));
            vars["--hero-0-from"] = Get("--deep-950");
            vars["--hero-0-via"] = Get("--deep-800");
            vars["--hero-0-to"] = Get("--brand-500");
        }
        if (vars.Keys.Any(k => k.StartsWith("--support-")))
        {
            vars["--hero-1-glow"] = Get("--support-400");
            vars["--hero-1-from"] = Get("--support-900");
            vars["--hero-1-via"] = Get("--support-800");
            vars["--hero-1-to"] = Get("--support-500");
        }
        if (vars.Keys.Any(k => k.StartsWith("--highlight-")))
        {
            var h = Oklab.FromHex(Get("--highlight-500"));
            vars["--hero-2-glow"] = h.Tint(.35, .9).ToHex();
            vars["--hero-2-from"] = h.Scale(.35, .6).ToHex();
            vars["--hero-2-via"] = Get("--highlight-800");
            vars["--hero-2-to"] = Get("--highlight-500");
        }

        return new Result(vars, adjustments, problems);
    }

    private static string? Valid(string? hex)
    {
        hex = hex?.Trim();
        return !string.IsNullOrEmpty(hex) && ThemeResolver.HexColor().IsMatch(hex) ? hex.ToUpperInvariant() : null;
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static bool Lighter(string a, string b) => Oklab.FromHex(a).L >= Oklab.FromHex(b).L;

    /// <summary>Lowers OKLab lightness until <paramref name="hex"/> reaches <paramref name="min"/> against <paramref name="other"/>.</summary>
    internal static string Darken(string hex, string other, double min) => Shift(hex, other, min, -.005);
    internal static string Lighten(string hex, string other, double min) => Shift(hex, other, min, +.005);

    private static string Shift(string hex, string other, double min, double step)
    {
        var c = Oklab.FromHex(hex);
        var result = hex.ToUpperInvariant();
        for (var i = 0; i < 200 && Contrast.Ratio(result, other) < min; i++)
        {
            c = c.WithLightness(Math.Clamp(c.L + step, 0, 1));
            result = c.ToHex();
        }
        return result;
    }
}

/// <summary>OKLab colour (Björn Ottosson): perceptually even mixing and lightening, converted from / to sRGB hex.</summary>
internal readonly record struct Oklab(double L, double A, double B)
{
    public static readonly Oklab White = FromHex("#FFFFFF");
    public static readonly Oklab Black = FromHex("#000000");

    public Oklab Mix(Oklab other, double t) => new(L + (other.L - L) * t, A + (other.A - A) * t, B + (other.B - B) * t);
    public Oklab WithLightness(double l) => this with { L = l };
    public Oklab Desaturate(double keep) => new(L, A * keep, B * keep);
    /// <summary>Moves <paramref name="toWhite"/> of the way to white; chroma × <paramref name="chroma"/>, hue kept.</summary>
    public Oklab Tint(double toWhite, double chroma) => new(L + (1 - L) * toWhite, A * chroma, B * chroma);
    /// <summary>Lightness × <paramref name="lightness"/>, chroma × <paramref name="chroma"/>, hue kept.</summary>
    public Oklab Scale(double lightness, double chroma) => new(Math.Clamp(L * lightness, 0, 1), A * chroma, B * chroma);

    public static Oklab FromHex(string hex)
    {
        double Lin(int i)
        {
            var v = int.Parse(hex.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        double r = Lin(1), g = Lin(3), b = Lin(5);
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return new(0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
                   1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
                   0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    public string ToHex()
    {
        var l = Math.Pow(L + 0.3963377774 * A + 0.2158037573 * B, 3);
        var m = Math.Pow(L - 0.1055613458 * A - 0.0638541728 * B, 3);
        var s = Math.Pow(L - 0.0894841775 * A - 1.2914855480 * B, 3);
        double r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        double g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        double b = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
        static int Channel(double v)
        {
            v = Math.Clamp(v, 0, 1);
            v = v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
            return (int)Math.Round(Math.Clamp(v, 0, 1) * 255);
        }
        return string.Create(CultureInfo.InvariantCulture, $"#{Channel(r):X2}{Channel(g):X2}{Channel(b):X2}");
    }
}
