using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EHC.Web.Blocks;

/// <summary>
/// Works on stored block list / block grid values (nested ones included) across languages.
/// Pages share one list of blocks; each block keeps its texts per language and is only shown in a language it is
/// "exposed" in (Umbraco 15+). These helpers fill a language from another one and find blocks a language is missing.
/// </summary>
public static class BlockLanguages
{
    private static readonly JsonSerializerOptions Write = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public sealed record Result(string Json, int Values, int Blocks);

    /// <summary>
    /// For every block: copies each language-specific value that is filled in <paramref name="from"/> and empty in
    /// <paramref name="to"/>, and exposes in <paramref name="to"/> the blocks exposed in <paramref name="from"/>.
    /// Never overwrites a filled value. Null when nothing changes.
    /// </summary>
    public static Result? CopyMissing(string? json, string from, string to)
    {
        if (Parse(json) is not JsonObject root) return null;
        int values = 0, blocks = 0;
        return Copy(root, from, to, ref values, ref blocks) ? new Result(root.ToJsonString(Write), values, blocks) : null;
    }

    private static bool Copy(JsonObject list, string from, string to, ref int values, ref int blocks)
    {
        var changed = false;
        foreach (var item in Items(list))
        {
            if (item["values"] is not JsonArray vals) continue;
            foreach (var group in vals.OfType<JsonObject>().Where(v => v["segment"] is null).GroupBy(v => Str(v["alias"])).ToList())
            {
                var source = group.FirstOrDefault(v => Str(v["culture"]) == from);
                if (source is null || IsEmpty(source["value"])) continue;
                var target = group.FirstOrDefault(v => Str(v["culture"]) == to);
                if (target is not null && !IsEmpty(target["value"])) continue;
                if (target is null)
                {
                    target = (JsonObject)source.DeepClone();
                    target["culture"] = to;
                    vals.Add(target);
                }
                else target["value"] = source["value"]?.DeepClone();
                values++;
                changed = true;
            }
            // nested block lists (slides, panel items, cards…)
            foreach (var v in vals.OfType<JsonObject>())
            {
                if (Nested(v["value"]) is not { } inner || !Copy(inner.List, from, to, ref values, ref blocks)) continue;
                v["value"] = inner.AsString ? inner.List.ToJsonString(Write) : inner.List;
                changed = true;
            }
        }
        if (list["expose"] is JsonArray expose)
        {
            var entries = expose.OfType<JsonObject>().ToList();
            foreach (var key in entries.Where(e => Str(e["culture"]) == from && e["segment"] is null).Select(e => Str(e["contentKey"])).Distinct().ToList())
            {
                if (entries.Any(e => Str(e["contentKey"]) == key && Str(e["culture"]) == to)) continue;
                expose.Add(new JsonObject { ["contentKey"] = key, ["culture"] = to, ["segment"] = null });
                blocks++;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>One block missing a language: its element type and the language it has no text (or is not shown) in.</summary>
    public sealed record Gap(Guid ContentTypeKey, string Culture);

    /// <summary>
    /// Blocks that have text in some of <paramref name="cultures"/> but not in others, or are shown (exposed) in some
    /// and not in others. Invariant blocks (no language-specific values) are never reported.
    /// </summary>
    public static IReadOnlyList<Gap> Gaps(string? json, IReadOnlyCollection<string> cultures)
    {
        var gaps = new List<Gap>();
        if (Parse(json) is JsonObject root && cultures.Count > 1) Find(root, cultures, gaps);
        return gaps;
    }

    private static void Find(JsonObject list, IReadOnlyCollection<string> cultures, List<Gap> gaps)
    {
        var exposed = (list["expose"] as JsonArray)?.OfType<JsonObject>()
            .GroupBy(e => Str(e["contentKey"])).ToDictionary(g => g.Key, g => g.Select(e => Str(e["culture"])).ToHashSet()) ?? [];
        foreach (var item in (list["contentData"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (item["values"] is not JsonArray vals) continue;
            var withText = vals.OfType<JsonObject>()
                .Where(v => Str(v["culture"]).Length > 0 && Nested(v["value"]) is null && !IsEmpty(v["value"]))
                .Select(v => Str(v["culture"])).ToHashSet();
            var shown = (exposed.GetValueOrDefault(Str(item["key"])) ?? []).Where(c => c.Length > 0).ToHashSet();   // "" = invariant block
            if ((withText.Count > 0 || shown.Count > 0) && Guid.TryParse(Str(item["contentTypeKey"]), out var type))
            {
                foreach (var c in cultures)
                    if ((withText.Count > 0 && !withText.Contains(c)) || (shown.Count > 0 && !shown.Contains(c)))
                        gaps.Add(new Gap(type, c));
            }
            foreach (var v in vals.OfType<JsonObject>())
                if (Nested(v["value"]) is { } inner) Find(inner.List, cultures, gaps);
        }
    }

    private sealed record NestedList(JsonObject List, bool AsString);

    /// <summary>A value that is itself a block list (stored as a JSON string or as an object).</summary>
    private static NestedList? Nested(JsonNode? value)
    {
        if (value is JsonObject o && o["contentData"] is JsonArray) return new(o, false);
        if (value is JsonValue jv && jv.TryGetValue<string>(out var s) && s.Contains("\"contentData\"", StringComparison.Ordinal)
            && Parse(s) is JsonObject p && p["contentData"] is JsonArray) return new(p, true);
        return null;
    }

    private static IEnumerable<JsonObject> Items(JsonObject list) =>
        new[] { list["contentData"], list["settingsData"] }.OfType<JsonArray>().SelectMany(a => a.OfType<JsonObject>());

    private static bool IsEmpty(JsonNode? value) => value switch
    {
        null => true,
        JsonValue v when v.TryGetValue<string>(out var s) => string.IsNullOrWhiteSpace(s) || s.Trim() is "[]" or "{}" or "null",
        JsonArray a => a.Count == 0,
        _ => false,
    };

    private static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

    private static JsonNode? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json); }
        catch (JsonException) { return null; }
    }
}
