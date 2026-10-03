using System.Text.Json.Nodes;
using EHC.Web.Blocks;
using EHC.Web.Composers;

public class HeroCampaignTests
{
    static SlidePanel Slide(bool hide = false, bool blocks = false) => new(hide, blocks);

    [Fact]
    public void Slide_blocks_win_then_hero_blocks()
    {
        Assert.Equal(["slide", "hero", "none"], HeroCampaign.Sources(new HeroPanel(true), [Slide(blocks: true), Slide(), Slide(hide: true, blocks: true)], null, false));
        Assert.Equal(["slide", "none"], HeroCampaign.Sources(new HeroPanel(false), [Slide(blocks: true), Slide()], null, false));
    }

    [Theory]
    [InlineData("none", true, "none")]
    [InlineData("art", false, "art")]
    [InlineData("cards", false, "cards")]
    [InlineData("navigator", true, "navigator")]
    [InlineData("navigator", false, "hero")]   // no care navigator on the page
    public void A_customizer_override_wins_over_every_slide(string locked, bool navigator, string expected) =>
        Assert.All(HeroCampaign.Sources(new HeroPanel(true), [Slide(blocks: true), Slide(hide: true)], locked, navigator), s => Assert.Equal(expected, s));

    [Theory]
    [InlineData(false, null, true)]
    [InlineData(true, null, false)]
    [InlineData(true, "show", true)]
    [InlineData(false, "hide", false)]
    [InlineData(true, "other", false)]
    public void Mark_follows_the_slide_then_the_hero(bool heroHides, string? slide, bool expected) =>
        Assert.Equal(expected, HeroCampaign.ShowMark(heroHides, slide));

    [Fact]
    public void Every_right_panel_item_has_a_view()
    {
        var root = RepoDir();
        var config = File.ReadAllText(Path.Combine(root, "src", "EHC.Web", "uSync", "v17", "DataTypes", "EHCHeroPanel.config"));
        var keys = System.Text.RegularExpressions.Regex.Matches(config, "\"contentElementTypeKey\": \"([0-9a-f-]{36})\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(15, keys.Count);
        var aliases = Directory.GetFiles(Path.Combine(root, "src", "EHC.Web", "uSync", "v17", "ContentTypes"), "*.config")
            .Select(File.ReadAllText)
            .Select(x => System.Text.RegularExpressions.Regex.Match(x, "<ContentType Key=\"([^\"]+)\" Alias=\"([^\"]+)\""))
            .Where(m => m.Success).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        foreach (var key in keys)
        {
            var alias = aliases[key];
            if (alias is "panelArt" or "panelCards" or "careNavigatorBlock") continue;   // rendered by _Panel itself
            Assert.Contains(alias, HeroCampaign.PanelItems);
            Assert.True(File.Exists(Path.Combine(root, "src", "EHC.Web", "Views", "Partials", "heroPanel", alias + ".cshtml")), alias);
        }
    }

    static string RepoDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "EHC.slnx"))) return dir.FullName;
        Assert.Fail("repository root not found");
        return "";
    }

    [Theory]
    [InlineData(null, "fade")]
    [InlineData("Fade", "fade")]
    [InlineData("Slide", "slide")]
    [InlineData("Rise", "rise")]
    [InlineData("Zoom", "zoom")]
    [InlineData("Blur", "blur")]
    [InlineData("spin\" onclick=\"x", "fade")]
    public void Transition_maps_through_an_allow_list(string? setting, string expected) => Assert.Equal(expected, HeroCampaign.Transition(setting));

    [Fact]
    public void Every_transition_has_its_css()
    {
        var css = File.ReadAllText(Path.Combine(RepoDir(), "frontend", "src", "css", "ehc.css"));
        foreach (var t in new[] { "slide", "rise", "zoom", "blur" })
            Assert.Contains($"[data-transition=\"{t}\"]", css);
    }

    // ------------------------------------------------------------------ conversion of the old fixed settings

    static readonly Guid HeroKey = Guid.NewGuid(), SlideKey = Guid.NewGuid(), ArtKey = Guid.NewGuid(), CardsKey = Guid.NewGuid(), CardKey = Guid.NewGuid();
    static readonly HeroMigrationKeys Keys = new(HeroKey, SlideKey, ArtKey, CardsKey, new Dictionary<string, string> { ["1"] = "umb://document/teal" });

    static JsonObject V(string alias, string? value, string? culture = null) => new() { ["editorAlias"] = "x", ["culture"] = culture, ["segment"] = null, ["alias"] = alias, ["value"] = value };

    static string List(params (Guid Type, string Key, JsonObject[] Values)[] items) => new JsonObject
    {
        ["contentData"] = new JsonArray([.. items.Select(i => (JsonNode)new JsonObject { ["contentTypeKey"] = i.Type.ToString(), ["key"] = i.Key, ["values"] = new JsonArray([.. i.Values]) })]),
        ["settingsData"] = new JsonArray(),
        ["expose"] = new JsonArray([.. items.SelectMany(i => new[] { "ar-SA", "en-US" }.Select(c => (JsonNode)new JsonObject { ["contentKey"] = i.Key, ["culture"] = c, ["segment"] = null }))]),
        ["layout"] = new JsonObject { ["Umbraco.BlockList"] = new JsonArray([.. items.Select(i => (JsonNode)new JsonObject { ["contentKey"] = i.Key, ["settingsKey"] = null })]) },
    }.ToJsonString();

    static string Page(JsonObject[] heroValues) => List((HeroKey, "h1", heroValues));

    static JsonArray HeroValues(string json) => (JsonArray)JsonNode.Parse(json)!["contentData"]![0]!["values"]!;
    static string? Get(JsonArray values, string alias) => values.OfType<JsonObject>().FirstOrDefault(v => v["alias"]!.GetValue<string>() == alias)?["value"]?.GetValue<string>();
    static JsonArray Items(string? blockList) => (JsonArray)JsonNode.Parse(blockList!)!["contentData"]!;

    [Fact]
    public void Old_hero_and_slide_settings_are_converted()
    {
        var cards = List((CardKey, "c1", [V("title", "Deliver with compassion", "en-US")]));
        var slides = List(
            (SlideKey, "s1", [V("backgroundPreset", "[\"1\"]"), V("title", "One shot.", "en-US")]),
            (SlideKey, "s2", [V("backgroundPreset", "[\"0\"]"), V("rightPanel", "[\"none\"]")]),
            (SlideKey, "s3", [V("backgroundPreset", "[\"2\"]"), V("rightPanel", "[\"cards\"]")]));
        var json = HeroSettingsMigration.Migrate(Page([V("rightPanel", "[\"art\"]"), V("floatingCards", cards), V("slides", slides)]), Keys, out var skipped);

        Assert.False(skipped);
        var hero = HeroValues(json!);
        Assert.Null(Get(hero, "rightPanel"));
        Assert.Null(Get(hero, "floatingCards"));
        var panel = Items(Get(hero, "panel"));
        Assert.Equal([ArtKey.ToString(), CardsKey.ToString()], panel.Select(p => p!["contentTypeKey"]!.GetValue<string>()));
        Assert.Contains("Deliver with compassion", panel[1]!["values"]![0]!["value"]!.GetValue<string>());
        var expose = (JsonArray)JsonNode.Parse(Get(hero, "panel")!)!["expose"]!;
        Assert.Equal(3, expose.Count);   // art: invariant (1), cards: ar-SA + en-US

        var s = Items(Get(hero, "slides"));
        var s1 = (JsonArray)s[0]!["values"]!;
        Assert.Equal("umb://document/teal", Get(s1, "gradient"));
        Assert.Null(Get(s1, "backgroundPreset"));
        var s2 = (JsonArray)s[1]!["values"]!;
        Assert.Equal("1", Get(s2, "hidePanel"));
        Assert.Null(Get(s2, "rightPanel"));
        Assert.Null(Get(s2, "backgroundPreset"));   // 0 is the default
        var s3 = (JsonArray)s[2]!["values"]!;
        Assert.Equal("[\"2\"]", Get(s3, "backgroundPreset"));   // no Rose gradient known: kept, nothing lost
        Assert.Equal([CardsKey.ToString()], Items(Get(s3, "panel")).Select(p => p!["contentTypeKey"]!.GetValue<string>()));

        Assert.Null(HeroSettingsMigration.Migrate(json!, Keys with { PresetGradients = new Dictionary<string, string>() }, out _));   // runs once
    }

    [Fact]
    public void Existing_new_settings_are_kept()
    {
        var panel = List((ArtKey, "a1", []));
        var json = HeroSettingsMigration.Migrate(Page([V("rightPanel", "[\"cards\"]"), V("panel", panel)]), Keys, out _);
        Assert.Equal(panel, Get(HeroValues(json!), "panel"));
    }

    [Fact]
    public void Old_navigator_is_left_alone()
    {
        Assert.Null(HeroSettingsMigration.Migrate(Page([V("rightPanel", "[\"navigator\"]")]), Keys, out var skipped));
        Assert.True(skipped);
    }

    [Fact]
    public void Heroes_nested_in_other_blocks_are_converted()
    {
        var inner = Page([V("rightPanel", "[\"none\"]")]);
        var outer = List((Guid.NewGuid(), "col", [V("items", inner)]));
        var json = HeroSettingsMigration.Migrate(outer, Keys, out _);
        var nested = Get((JsonArray)JsonNode.Parse(json!)!["contentData"]![0]!["values"]!, "items");
        Assert.Null(Get(HeroValues(nested!), "rightPanel"));
    }
}
