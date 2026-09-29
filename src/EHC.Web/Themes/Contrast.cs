using System.Globalization;

namespace EHC.Web.Themes;

/// <summary>WCAG 2.x contrast ratio, mirroring frontend/scripts/check-contrast.mjs. The rules live in <see cref="Palette.Rules"/>.</summary>
public static class Contrast
{
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
}
