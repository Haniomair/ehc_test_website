using System.Text.RegularExpressions;
using EHC.Web.Themes;

public class PaletteTests
{
    private static readonly Dictionary<string, string?> NoTuning = new();

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EHC.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine([dir!.FullName, .. parts]);
    }

    [Fact]
    public void Defaults_match_the_css_tokens()
    {
        var css = File.ReadAllText(RepoFile("frontend", "src", "css", "_tokens.css")).Split("@theme")[0];
        var tokens = Regex.Matches(css, @"(--[a-z0-9-]+):\s*(#[0-9A-Fa-f]{6})")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.ToUpperInvariant());
        foreach (var (name, hex) in Palette.Defaults)
            Assert.True(tokens.TryGetValue(name, out var css1) && css1 == hex, $"{name}: C# {hex}, CSS {css1}");
    }

    [Fact]
    public void No_colours_means_no_variables()
    {
        var r = Palette.Build(new ThemeColors(null, "", "red", "#abc", "#000000;x:y"), NoTuning);
        Assert.Empty(r.Variables);
        Assert.Empty(r.Problems);
    }

    [Theory]
    [InlineData("#007A3D", "#07301E", "#C9A227")]
    [InlineData("#FFE600", null, null)]          // very light primary
    [InlineData("#111111", "#F0F0F0", "#1A1A1A")] // dark primary, pale "dark", dark accent
    [InlineData("#D6457A", null, "#FFFFFF")]
    public void Generated_palettes_are_always_readable(string primary, string? dark, string? accent)
    {
        var r = Palette.Build(new ThemeColors(primary, dark, accent, "#C2569B", "#0E8F87"), NoTuning);
        Assert.Empty(r.Problems);
        string Get(string v) => v.StartsWith('#') ? v : r.Variables.GetValueOrDefault(v) ?? Palette.Defaults[v];
        foreach (var (label, text, bg, min) in Palette.Rules)
            Assert.True(Contrast.Ratio(Get(text), Get(bg)) >= min, $"{label} with {primary}/{dark}/{accent}");
    }

    [Fact]
    public void Random_colours_are_always_readable()
    {
        var rng = new Random(42);
        string Hex() => $"#{rng.Next(0x1000000):X6}";
        for (var i = 0; i < 300; i++)
        {
            var r = Palette.Build(new ThemeColors(Hex(), Hex(), Hex(), Hex(), Hex()), NoTuning);
            Assert.Empty(r.Problems);
        }
    }

    [Fact]
    public void Base_colour_is_kept_and_all_output_is_hex()
    {
        var r = Palette.Build(new ThemeColors("#007a3d", null, null, null, null), NoTuning);
        Assert.Equal("#007A3D", r.Variables["--brand-500"]);
        Assert.All(r.Variables.Values, v => Assert.Matches("^#[0-9A-F]{6}$", v));
        Assert.Contains("--deep-900", r.Variables.Keys);   // dark family derived from the primary
        Assert.Contains("--hero-0-to", r.Variables.Keys);  // explicit, so section themes work
    }

    [Fact]
    public void Unreadable_accent_is_adjusted_and_reported()
    {
        var r = Palette.Build(new ThemeColors(null, null, "#555555", null, null), NoTuning);
        Assert.NotEqual("#555555", r.Variables["--accent-400"]);
        Assert.Single(r.Adjustments);
    }

    [Fact]
    public void Unreadable_fine_tune_is_a_problem_not_a_silent_change()
    {
        var r = Palette.Build(new ThemeColors(null, null, null, null, null), new Dictionary<string, string?> { ["brand600"] = "#2490CC" });
        Assert.Equal("#2490CC", r.Variables["--brand-600"]);
        Assert.Single(r.Problems);
    }

    [Fact]
    public void Fine_tune_replaces_a_generated_shade()
    {
        var r = Palette.Build(new ThemeColors("#6B3FA0", null, null, null, null), new Dictionary<string, string?> { ["brand300"] = "#E9C77B" });
        Assert.Equal("#E9C77B", r.Variables["--brand-300"]);
    }

    [Fact]
    public void Only_allow_listed_fine_tunes_are_used()
    {
        var r = Palette.Build(new ThemeColors(null, null, null, null, null), new Dictionary<string, string?> { ["background"] = "#FF0000", ["brand50"] = "url(x)" });
        Assert.Empty(r.Variables);
    }
}
