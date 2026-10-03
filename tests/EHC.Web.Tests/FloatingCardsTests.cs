using System.Text.RegularExpressions;
using EHC.Web.Blocks;

public class FloatingCardsTests
{
    [Fact]
    public void Defaults_for_empty_or_unknown_values()
    {
        var f = FloatingCards.Create([], null, "Spin!", "", "<b>", false);
        Assert.Equal("fc-list fc-glass fc-motion-none fc-enter-none fc-layout-stacked", f.ListClass);
    }

    [Fact]
    public void Editor_choices_map_to_fixed_classes()
    {
        var f = FloatingCards.Create([], "Tinted glass", "Drift", "Slide in", "Scattered", true);
        Assert.Equal("fc-list fc-tinted fc-motion-drift fc-enter-slide fc-layout-scattered fc-hover", f.ListClass);
    }

    [Fact]
    public void Every_dropdown_option_has_a_css_rule()
    {
        var root = Root();
        var css = File.ReadAllText(Path.Combine(root, "frontend", "src", "css", "ehc.css"));
        foreach (var (file, prefix) in new[] { ("EHCCardGlass", "fc-"), ("EHCCardMotion", "fc-motion-"), ("EHCCardEntrance", "fc-enter-"), ("EHCCardLayout", "fc-layout-") })
        {
            var config = File.ReadAllText(Path.Combine(root, "src", "EHC.Web", "uSync", "v17", "DataTypes", file + ".config"));
            var items = Regex.Matches(config, @"^\s+""([^""]+)"",?\r?$", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
            Assert.NotEmpty(items);
            foreach (var item in items)
            {
                var cls = prefix switch
                {
                    "fc-" => FloatingCards.Create([], item, null, null, null, false).Style,
                    "fc-motion-" => FloatingCards.Create([], null, item, null, null, false).Motion,
                    "fc-enter-" => FloatingCards.Create([], null, null, item, null, false).Entrance,
                    _ => FloatingCards.Create([], null, null, null, item, false).Layout,
                };
                if (cls == "none") continue;   // no rule needed
                Assert.True(css.Contains("." + prefix + cls + " ", StringComparison.Ordinal), $"{item} → .{prefix}{cls} has no CSS rule");
            }
        }
    }

    static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "EHC.slnx"))) return dir.FullName;
        Assert.Fail("repository root not found");
        return "";
    }
}
