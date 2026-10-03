using System.Text.Json.Nodes;
using EHC.Web.Blocks;

public class BlockLanguagesTests
{
    const string En = "en-US", Ar = "ar-SA";
    static readonly Guid Hero = Guid.NewGuid(), Slide = Guid.NewGuid(), Art = Guid.NewGuid();

    static JsonObject V(string alias, object? value, string? culture) => new() { ["editorAlias"] = "x", ["culture"] = culture, ["segment"] = null, ["alias"] = alias, ["value"] = JsonValue.Create(value) };

    static string List((Guid Type, string Key, JsonObject[] Values, string?[] Exposed)[] items) => new JsonObject
    {
        ["contentData"] = new JsonArray([.. items.Select(i => (JsonNode)new JsonObject { ["contentTypeKey"] = i.Type.ToString(), ["key"] = i.Key, ["values"] = new JsonArray([.. i.Values]) })]),
        ["settingsData"] = new JsonArray(),
        ["expose"] = new JsonArray([.. items.SelectMany(i => i.Exposed.Select(c => (JsonNode)new JsonObject { ["contentKey"] = i.Key, ["culture"] = c, ["segment"] = null }))]),
        ["layout"] = new JsonObject { ["Umbraco.BlockList"] = new JsonArray([.. items.Select(i => (JsonNode)new JsonObject { ["contentKey"] = i.Key })]) },
    }.ToJsonString();

    static string Page()
    {
        var slides = List([
            (Slide, "s1", [V("title", "Your health", En), V("title", "صحتك", Ar), V("gradient", "umb://document/x", null)], [En, Ar]),
            (Slide, "s2", [V("title", "New slide", En), V("lead", "Lead", En), V("title", "", Ar)], [En]),
        ]);
        var panel = List([(Art, "a1", [], [null])]);
        return List([(Hero, "h1", [V("slides", slides, null), V("panel", panel, null), V("autoRotateSeconds", 7, null)], [En, Ar])]);
    }

    static JsonObject Item(string json, params string[] path)
    {
        JsonNode node = JsonNode.Parse(json)!;
        foreach (var key in path)
        {
            var item = node["contentData"]!.AsArray().First(i => i!["key"]!.GetValue<string>() == key)!;
            node = item;
            if (key != path[^1]) node = JsonNode.Parse(item["values"]!.AsArray().First(v => v!["value"] is JsonValue jv && jv.TryGetValue<string>(out var s) && s.Contains(path[Array.IndexOf(path, key) + 1]))!["value"]!.GetValue<string>())!;
        }
        return (JsonObject)node;
    }

    static string? Text(JsonObject item, string alias, string culture) =>
        item["values"]!.AsArray().FirstOrDefault(v => v!["alias"]!.GetValue<string>() == alias && v["culture"]?.GetValue<string>() == culture)?["value"]?.GetValue<string>();

    [Fact]
    public void Fills_empty_texts_and_shows_the_blocks_in_the_other_language()
    {
        var r = BlockLanguages.CopyMissing(Page(), En, Ar)!;
        Assert.Equal(2, r.Values);   // s2: title (empty in Arabic) and lead (missing)
        Assert.Equal(1, r.Blocks);   // s2 is now shown in Arabic
        var s1 = Item(r.Json, "h1", "s1");
        var s2 = Item(r.Json, "h1", "s2");
        Assert.Equal("صحتك", Text(s1, "title", Ar));   // never overwritten
        Assert.Equal("New slide", Text(s2, "title", Ar));
        Assert.Equal("Lead", Text(s2, "lead", Ar));
        var slides = JsonNode.Parse(JsonNode.Parse(r.Json)!["contentData"]![0]!["values"]![0]!["value"]!.GetValue<string>())!;
        Assert.Contains(slides["expose"]!.AsArray(), e => e!["contentKey"]!.GetValue<string>() == "s2" && e["culture"]!.GetValue<string>() == Ar);
    }

    [Fact]
    public void Invariant_values_and_invariant_blocks_are_left_alone()
    {
        var r = BlockLanguages.CopyMissing(Page(), En, Ar)!;
        var hero = JsonNode.Parse(r.Json)!["contentData"]![0]!["values"]!.AsArray();
        Assert.Single(hero, v => v!["alias"]!.GetValue<string>() == "autoRotateSeconds");
        var panel = JsonNode.Parse(hero.First(v => v!["alias"]!.GetValue<string>() == "panel")!["value"]!.GetValue<string>())!;
        Assert.Single(panel["expose"]!.AsArray());   // the logo art block stays shown for all languages
    }

    [Fact]
    public void Runs_once()
    {
        var r = BlockLanguages.CopyMissing(Page(), En, Ar)!;
        Assert.Null(BlockLanguages.CopyMissing(r.Json, En, Ar));
    }

    [Fact]
    public void Gaps_report_blocks_missing_a_language()
    {
        var gaps = BlockLanguages.Gaps(Page(), [En, Ar]);
        Assert.Equal([new BlockLanguages.Gap(Slide, Ar)], gaps);
        var fixedJson = BlockLanguages.CopyMissing(Page(), En, Ar)!.Json;
        Assert.Empty(BlockLanguages.Gaps(fixedJson, [En, Ar]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public void Bad_values_do_nothing(string? json)
    {
        Assert.Null(BlockLanguages.CopyMissing(json, En, Ar));
        Assert.Empty(BlockLanguages.Gaps(json, [En, Ar]));
    }
}
