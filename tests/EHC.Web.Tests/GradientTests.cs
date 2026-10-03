using System.Text.RegularExpressions;
using EHC.Web.Blocks;
using EHC.Web.Themes;

public class GradientTests
{
    [Theory]
    [InlineData("Primary 500", "--brand-500")]
    [InlineData("Deep 900", "--deep-900")]
    [InlineData("Support 300", "--support-300")]
    [InlineData("[\"Highlight 800\"]", "--highlight-800")]   // raw dropdown value while saving
    [InlineData("Primary 999", null)]
    [InlineData("Brand 500", null)]
    [InlineData("Primary", null)]
    [InlineData("red;}", null)]
    [InlineData(null, null)]
    public void Theme_labels_map_through_the_allow_list(string? label, string? expected) => Assert.Equal(expected, Gradients.ThemeVar(label));

    [Theory]
    [InlineData("#123abc", "#123ABC")]
    [InlineData("Deep 900", "var(--deep-900)")]
    [InlineData("#12", null)]
    [InlineData("red", null)]
    [InlineData("#123456;}body{x:y", null)]
    [InlineData("var(--deep-900)", null)]
    [InlineData(null, null)]
    public void Colours_are_hex_or_known_theme_colours(string? colour, string? expected) => Assert.Equal(expected, Gradients.Colour(colour));

    [Fact]
    public void Parse_cleans_the_stored_design()
    {
        var d = Gradients.Parse("""
            {"angle":-45,"stops":[{"at":120,"colour":"Primary 500"},{"at":"10","colour":"#0a2a4f"},{"at":5,"colour":"red;}"},{"at":50,"colour":null}],
             "glow":{"colour":"Deep 900","x":180,"y":-5,"strength":40}}
            """)!;
        Assert.Equal(315, d.Angle);
        Assert.Equal([new GradientStop(10, "#0a2a4f"), new GradientStop(100, "Primary 500")], d.Stops);
        Assert.Equal(new GradientGlow("Deep 900", 100, 0, 40), d.Glow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"angle\":90,\"stops\":[{\"at\":0,\"colour\":\"Deep 900\"}]}")]
    [InlineData("{\"angle\":90,\"stops\":[{\"at\":0,\"colour\":\"Deep 900\"},{\"at\":100,\"colour\":\"url(x)\"}]}")]
    public void Unusable_designs_are_rejected(string? json) => Assert.Null(Gradients.Parse(json));

    [Fact]
    public void At_most_eight_stops()
    {
        var stops = string.Join(",", Enumerable.Range(0, 12).Select(i => $"{{\"at\":{i * 8},\"colour\":\"Deep 900\"}}"));
        Assert.Equal(Gradients.MaxStops, Gradients.Parse($"{{\"angle\":0,\"stops\":[{stops}]}}")!.Stops.Count);
    }

    [Fact]
    public void Published_json_values_are_read_as_text()
    {
        const string json = "{\"angle\":90,\"stops\":[{\"at\":0,\"colour\":\"Deep 900\"},{\"at\":100,\"colour\":\"Primary 500\"}]}";
        using var doc = System.Text.Json.JsonDocument.Parse(json);   // what Umbraco's JSON converter returns
        Assert.NotNull(Gradients.Parse(Gradients.Raw(doc)));
        Assert.NotNull(Gradients.Parse(Gradients.Raw(doc.RootElement)));
        Assert.NotNull(Gradients.Parse(Gradients.Raw(System.Text.Json.Nodes.JsonNode.Parse(json))));
        Assert.NotNull(Gradients.Parse(Gradients.Raw(json)));
        Assert.Null(Gradients.Raw(null));
    }

    [Fact]
    public void Css_is_built_from_validated_parts()
    {
        var plain = new GradientDesign(90, [new(0, "Deep 900"), new(37.5, "#0e8f87"), new(100, "Primary 500")], null);
        Assert.Equal("linear-gradient(90deg, var(--deep-900) 0%, #0E8F87 37.5%, var(--brand-500) 100%)", Gradients.Css(plain));

        var glow = plain with { Glow = new("Primary 400", 80, 20, 60) };
        Assert.StartsWith("radial-gradient(900px 600px at 80% 20%, color-mix(in srgb, var(--brand-400) 60%, transparent) 0%, transparent 60%), linear-gradient(", Gradients.Css(glow));
        Assert.Contains("at 80% 20%, var(--brand-400) 0%", Gradients.Css(plain with { Glow = new("Primary 400", 80, 20, 100) }));
    }

    [Fact]
    public void Round_trip_through_json()
    {
        var d = new GradientDesign(135, [new(0, "Deep 900"), new(100, "#3A1030")], new("Primary 400", 80, 20, 100));
        var back = Gradients.Parse(Gradients.Json(d))!;
        Assert.Equal(d.Angle, back.Angle);
        Assert.Equal(d.Stops, back.Stops);
        Assert.Equal(d.Glow, back.Glow);
    }

    [Fact]
    public void Readability_checks_every_stop()
    {
        var r = Gradients.Readability(new GradientDesign(135, [new(0, "Deep 900"), new(50, "#FFFFFF"), new(100, "Primary 500")], null));
        Assert.Equal([1, 2, 3], r.Select(x => x.Stop));
        Assert.True(r[0].Ratio > 12);
        Assert.Equal(1, r[1].Ratio);
        Assert.InRange(r[2].Ratio, 3.0, 4.5);   // the EHC blue end: fine for headings, a warning for small text
    }

    [Fact]
    public void Editor_colours_match_the_palette()
    {
        var js = File.ReadAllText(RepoFile("src", "EHC.Web", "wwwroot", "App_Plugins", "EhcGradient", "gradient-editor.js"));
        var families = Regex.Matches(js, @"^\s+(\w+): \{([^}]*)\},?$", RegexOptions.Multiline);
        var found = 0;
        foreach (Match f in families)
        {
            foreach (Match shade in Regex.Matches(f.Groups[2].Value, @"(\d+): '(#[0-9A-F]{6})'"))
            {
                var label = $"{f.Groups[1].Value} {shade.Groups[1].Value}";
                var v = Gradients.ThemeVar(label);
                Assert.True(v is not null, $"unknown colour {label}");
                Assert.Equal(Palette.Defaults[v!], shade.Groups[2].Value);
                found++;
            }
        }
        Assert.Equal(Palette.Defaults.Count(k => Regex.IsMatch(k.Key, @"^--(brand|deep|night|accent|highlight|support)-\d+$")), found);
    }

    static string RepoFile(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine([dir.FullName, .. parts]);
            if (File.Exists(path)) return path;
        }
        Assert.Fail("Not found: " + Path.Combine(parts));
        return "";
    }
}
