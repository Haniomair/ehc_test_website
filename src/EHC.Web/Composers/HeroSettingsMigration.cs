using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace EHC.Web.Composers;

/// <summary>Element type keys and gradient picker values the conversion needs.</summary>
public sealed record HeroMigrationKeys(Guid Hero, Guid Slide, Guid PanelArt, Guid PanelCards, IReadOnlyDictionary<string, string> PresetGradients);

/// <summary>
/// One-time conversion of the campaign hero's old fixed settings into the current ones, on start, wherever a hero is
/// used (every block list value, nested ones included):
/// hero rightPanel art/cards + floatingCards → Right panel blocks (Logo art, Floating cards); slide rightPanel none →
/// Hide right panel; slide art/cards or own floatingCards → the slide's own Right panel; slide backgroundPreset 1/2 →
/// the Teal / Rose gradient when no gradient is picked. The old values are removed once converted, so it runs once.
/// A hero with the old "navigator" setting is left as it is (reported in the log).
/// </summary>
public sealed class HeroSettingsMigration(IContentService contents, IContentTypeService contentTypes, IRuntimeState runtime, ILogger<HeroSettingsMigration> logger)
    : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private static readonly JsonSerializerOptions Write = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        // a content surprise must never stop the site from starting
        try { Run(); }
        catch (Exception e) { logger.LogError(e, "Converting old hero settings failed; nothing more was changed"); }
    }

    private void Run()
    {
        if (runtime.Level != RuntimeLevel.Run) return;
        var hero = contentTypes.Get("heroCampaignBlock");
        var slide = contentTypes.Get("heroSlide");
        var art = contentTypes.Get("panelArt");
        var cards = contentTypes.Get("panelCards");
        // works on the stored values, so it still converts after the old properties are removed from the types
        if (hero is null || slide is null || art is null || cards is null) return;

        var presets = new Dictionary<string, string>();
        if (contentTypes.Get("gradient") is { } gradientType)
        {
            foreach (var g in contents.GetPagedOfTypes([gradientType.Id], 0, 200, out _, null, null).Where(g => !g.Trashed))
            {
                if (g.Name == "Teal") presets.TryAdd("1", Udi.Create(Constants.UdiEntityType.Document, g.Key).ToString());
                if (g.Name == "Rose") presets.TryAdd("2", Udi.Create(Constants.UdiEntityType.Document, g.Key).ToString());
            }
        }
        var keys = new HeroMigrationKeys(hero.Key, slide.Key, art.Key, cards.Key, presets);

        var blockTypes = contentTypes.GetAll().Where(t => !t.IsElement && t.CompositionPropertyTypes.Any(p => p.PropertyEditorAlias == Constants.PropertyEditors.Aliases.BlockList)).Select(t => t.Id).ToArray();
        if (blockTypes.Length == 0) return;
        var changed = 0;
        foreach (var page in contents.GetPagedOfTypes(blockTypes, 0, 10000, out _, null, null))
        {
            if (page.Trashed) continue;
            var wasPublishedAsIs = page.Published && !page.Edited;
            var any = false;
            foreach (var property in page.Properties.Where(p => p.PropertyType.PropertyEditorAlias == Constants.PropertyEditors.Aliases.BlockList))
            {
                foreach (var value in property.Values.ToList())
                {
                    if (value.EditedValue is not string json || !json.Contains(keys.Hero.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                    var result = Migrate(json, keys, out var skipped);
                    if (skipped) logger.LogWarning("Hero on {Page} uses the old care navigator panel and was not converted", page.Name);
                    if (result is null) continue;
                    page.SetValue(property.Alias, result, value.Culture, value.Segment);
                    any = true;
                }
            }
            if (!any) continue;
            contents.Save(page);
            if (wasPublishedAsIs) contents.Publish(page, page.AvailableCultures.Any() ? [.. page.AvailableCultures] : ["*"]);
            else if (page.Published) logger.LogWarning("Hero settings on {Page} converted in its draft only (it has unpublished changes): publish it to apply them", page.Name);
            changed++;
        }
        if (changed > 0) logger.LogInformation("Converted old hero settings on {Count} pages", changed);
    }

    /// <summary>The converted block list JSON, or null when nothing changed.</summary>
    public static string? Migrate(string json, HeroMigrationKeys keys, out bool skippedNavigator)
    {
        skippedNavigator = false;
        if (JsonNode.Parse(json) is not JsonObject root) return null;
        var skipped = false;
        var changed = Walk(root, keys, ref skipped);
        skippedNavigator = skipped;
        return changed ? root.ToJsonString(Write) : null;
    }

    private static bool Walk(JsonObject blockList, HeroMigrationKeys keys, ref bool skipped)
    {
        if (blockList["contentData"] is not JsonArray items) return false;
        var changed = false;
        foreach (var item in items.OfType<JsonObject>())
        {
            if (item["values"] is not JsonArray values) continue;
            if (Is(item, keys.Hero)) changed |= Hero(item, values, Cultures(blockList, item), keys, ref skipped);
            // nested block lists (sections inside columns, tabs…)
            foreach (var v in values.OfType<JsonObject>())
            {
                if (v["value"] is JsonValue jv && jv.TryGetValue<string>(out var s) && s.Contains("\"contentData\"", StringComparison.Ordinal) && JsonNode.Parse(s) is JsonObject inner && Walk(inner, keys, ref skipped))
                {
                    v["value"] = inner.ToJsonString(Write);
                    changed = true;
                }
            }
        }
        return changed;
    }

    private static bool Hero(JsonObject hero, JsonArray values, List<string?> cultures, HeroMigrationKeys keys, ref bool skipped)
    {
        var oldPanel = Single(Text(values, "rightPanel"));
        if (oldPanel == "navigator") { skipped = true; return false; }
        var changed = false;
        var heroCards = Text(values, "floatingCards");
        var heroKind = oldPanel ?? "cards";
        if (Has(values, "rightPanel") || Has(values, "floatingCards"))
        {
            if (!HasBlocks(Text(values, "panel")) && Panel(heroKind, heroCards, cultures, keys) is { } panel) Set(values, "panel", "Umbraco.BlockList", panel);
            Remove(values, "rightPanel");
            Remove(values, "floatingCards");
            changed = true;
        }

        if (Text(values, "slides") is not { } slidesJson || JsonNode.Parse(slidesJson) is not JsonObject slides || slides["contentData"] is not JsonArray slideItems) return changed;
        var slidesChanged = false;
        foreach (var slide in slideItems.OfType<JsonObject>().Where(s => Is(s, keys.Slide)))
        {
            if (slide["values"] is not JsonArray sv) continue;
            // background: old 1 / 2 → Teal / Rose when nothing is picked
            if (Has(sv, "backgroundPreset"))
            {
                var preset = Single(Text(sv, "backgroundPreset"));
                if (string.IsNullOrEmpty(Text(sv, "gradient")) && preset is not null && keys.PresetGradients.TryGetValue(preset, out var udi))
                    Set(sv, "gradient", "Umbraco.MultiNodeTreePicker", udi);
                // kept (for a later start) only while it is Teal / Rose and that gradient does not exist yet
                if (!string.IsNullOrEmpty(Text(sv, "gradient")) || preset is not ("1" or "2"))
                {
                    Remove(sv, "backgroundPreset");
                    slidesChanged = true;
                }
            }
            // panel
            if (Has(sv, "rightPanel") || Has(sv, "floatingCards"))
            {
                var slidePanel = Single(Text(sv, "rightPanel"));
                var ownCards = HasBlocks(Text(sv, "floatingCards")) ? Text(sv, "floatingCards") : null;
                var kind = slidePanel is "art" or "cards" or "none" ? slidePanel : ownCards is not null ? heroKind : null;
                if (kind == "none")
                {
                    Set(sv, "hidePanel", "Umbraco.TrueFalse", "1");
                }
                else if (kind is not null && !HasBlocks(Text(sv, "panel")) && Panel(kind, ownCards ?? heroCards, Cultures(slides, slide), keys) is { } panel)
                {
                    Set(sv, "panel", "Umbraco.BlockList", panel);
                }
                Remove(sv, "rightPanel");
                Remove(sv, "floatingCards");
                slidesChanged = true;
            }
        }
        if (slidesChanged)
        {
            Set(values, "slides", "Umbraco.BlockList", slides.ToJsonString(Write));
            changed = true;
        }
        return changed;
    }

    /// <summary>A Right panel block list: Logo art (for "art") and Floating cards holding the old cards.</summary>
    private static string? Panel(string kind, string? cardsJson, List<string?> cultures, HeroMigrationKeys keys)
    {
        var items = new List<(JsonObject Content, bool Variant)>();
        if (kind == "art") items.Add((new JsonObject { ["contentTypeKey"] = keys.PanelArt.ToString(), ["key"] = Guid.NewGuid().ToString(), ["values"] = new JsonArray() }, false));
        if (kind is "art" or "cards" && HasBlocks(cardsJson))
        {
            items.Add((new JsonObject
            {
                ["contentTypeKey"] = keys.PanelCards.ToString(),
                ["key"] = Guid.NewGuid().ToString(),
                ["values"] = new JsonArray(Value("cards", "Umbraco.BlockList", cardsJson!)),
            }, true));
        }
        if (items.Count == 0) return null;

        var expose = new JsonArray();
        var layout = new JsonArray();
        foreach (var (content, variant) in items)
        {
            var key = content["key"]!.GetValue<string>();
            foreach (var culture in variant ? cultures : [null])
                expose.Add(new JsonObject { ["contentKey"] = key, ["culture"] = culture, ["segment"] = null });
            layout.Add(new JsonObject { ["contentUdi"] = null, ["settingsUdi"] = null, ["contentKey"] = key, ["settingsKey"] = null });
        }
        return new JsonObject
        {
            ["contentData"] = new JsonArray([.. items.Select(i => (JsonNode)i.Content)]),
            ["settingsData"] = new JsonArray(),
            ["expose"] = expose,
            ["layout"] = new JsonObject { ["Umbraco.BlockList"] = layout },
        }.ToJsonString(Write);
    }

    /// <summary>The cultures an item is shown in (its "expose" entries); both site languages when none are listed.</summary>
    private static List<string?> Cultures(JsonObject blockList, JsonObject item)
    {
        var key = item["key"]?.GetValue<string>();
        var list = (blockList["expose"] as JsonArray)?.OfType<JsonObject>()
            .Where(e => string.Equals(e["contentKey"]?.GetValue<string>(), key, StringComparison.OrdinalIgnoreCase))
            .Select(e => e["culture"]?.GetValue<string>()).Where(c => c is not null).Distinct().ToList() ?? [];
        return list.Count > 0 ? list : ["ar-SA", "en-US"];
    }

    private static bool Is(JsonObject item, Guid type) => Guid.TryParse(item["contentTypeKey"]?.GetValue<string>(), out var k) && k == type;
    private static JsonObject? Find(JsonArray values, string alias) =>
        values.OfType<JsonObject>().FirstOrDefault(v => v["alias"]?.GetValue<string>() == alias && v["culture"] is null && v["segment"] is null);
    private static bool Has(JsonArray values, string alias) => Find(values, alias) is not null;
    private static string? Text(JsonArray values, string alias) => Find(values, alias)?["value"] switch
    {
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonNode n => n.ToJsonString(),
        _ => null,
    };
    private static void Remove(JsonArray values, string alias)
    {
        foreach (var v in values.OfType<JsonObject>().Where(v => v["alias"]?.GetValue<string>() == alias).ToList()) values.Remove(v);
    }
    private static void Set(JsonArray values, string alias, string editor, string value)
    {
        Remove(values, alias);
        values.Add(Value(alias, editor, value));
    }
    private static JsonObject Value(string alias, string editor, string value) =>
        new() { ["editorAlias"] = editor, ["culture"] = null, ["segment"] = null, ["alias"] = alias, ["value"] = value };
    private static bool HasBlocks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { return JsonNode.Parse(json)?["contentData"] is JsonArray { Count: > 0 }; }
        catch (JsonException) { return false; }
    }
    private static string? Single(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!raw.StartsWith('[')) return raw;
        try { return JsonSerializer.Deserialize<string[]>(raw)?.FirstOrDefault(); }
        catch (JsonException) { return null; }
    }
}
