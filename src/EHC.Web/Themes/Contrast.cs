using System.Globalization;

namespace EHC.Web.Themes;

/// <summary>WCAG 2.x contrast maths, mirroring frontend/scripts/check-contrast.mjs.</summary>
public static class Contrast
{
    public const string White = "#FFFFFF";
    public const string Soft = "#F4F8FC";          // --soft in frontend/src/css/_tokens.css
    public const string DarkOnAccent = "#2B1D00";  // text colour of the sand (accent) button

    /// <summary>Checks for each overridable theme colour: (alias, label, counterpart, minimum ratio).</summary>
    public static readonly (string Alias, string Label, string Against, double Min)[] Rules =
    [
        ("brand600", "white text on Brand 600 (primary buttons, links)", White, 4.5),
        ("brand500", "white on Brand 500 (large text, icons)", White, 3.0),
        ("deep900", "Deep 900 headings on the light section background", Soft, 7.0),
        ("deep900", "white text on Deep 900", White, 4.5),
        ("accent400", "dark text on Accent 400 (sand buttons)", DarkOnAccent, 4.5),
    ];

    public static double Ratio(string hexA, string hexB)
    {
        var a = Luminance(hexA);
        var b = Luminance(hexB);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance(string hex)
    {
        if (!ThemeResolver.HexColor().IsMatch(hex)) throw new ArgumentException($"Not a #RRGGBB colour: {hex}", nameof(hex));
        double Channel(int i)
        {
            var v = int.Parse(hex.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
    }

    /// <summary>Human-readable problems for the given overrides (alias → hex); empty when all pass.</summary>
    public static IReadOnlyList<string> Check(IReadOnlyDictionary<string, string?> overrides)
    {
        var problems = new List<string>();
        foreach (var (alias, label, against, min) in Rules)
        {
            if (!overrides.TryGetValue(alias, out var value) || string.IsNullOrWhiteSpace(value)) continue;
            value = value.Trim();
            if (!ThemeResolver.HexColor().IsMatch(value)) continue; // the property's regex validation reports this
            var ratio = Ratio(value, against);
            if (ratio < min)
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{label}: {ratio:0.00}:1, needs at least {min:0.0}:1 ({value})."));
        }
        return problems;
    }
}
