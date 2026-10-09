using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace EHC.Web.Site;

/// <summary>
/// Points link-picker entries that go to pages of the old site (ehc.med.sa) — or other outside pages this site now has its
/// own page for — at pages of this site. Used once by the
/// seeders that recreate those pages. Works on any property value: nested block values are stored as JSON inside JSON
/// strings (at any depth), so such strings are parsed, rewritten and stored back as strings.
/// </summary>
public static class LinkRewriter
{
    /// <summary>
    /// The decoded path of an old-site link without slashes and in lower case ("ar/تواصل-معنا", "privacy-policy"),
    /// or null when the link does not go to ehc.med.sa.
    /// </summary>
    public static string? OldSitePath(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.Host.EndsWith("ehc.med.sa", StringComparison.OrdinalIgnoreCase)) return null;
        return Uri.UnescapeDataString(uri.AbsolutePath).Trim('/').ToLowerInvariant();
    }

    private static readonly string[] OldSite = ["ehc.med.sa"];

    /// <summary>
    /// Rewrites every link whose url <paramref name="target"/> maps to a document; returns how many changed. JSON text is
    /// only parsed when it contains one of <paramref name="markers"/> (host names), default the old site.
    /// </summary>
    public static int Rewrite(JsonNode? node, Func<string, Guid?> target, params string[] markers)
    {
        if (markers.Length == 0) markers = OldSite;
        var count = 0;
        switch (node)
        {
            case JsonObject o when o["url"] is JsonValue v && v.TryGetValue<string>(out var url) && o.ContainsKey("name") && target(url) is { } key:
                o["target"] = null;
                o["unique"] = key.ToString();
                o["type"] = "document";
                o["udi"] = $"umb://document/{key:N}";
                o["url"] = null;
                o["queryString"] = null;
                return 1;
            case JsonObject o:
                foreach (var (name, child) in o.ToList())
                {
                    if (Nested(child, target, markers, out var n) is { } text) o[name] = text;
                    count += n;
                }
                break;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++)
                {
                    if (Nested(a[i], target, markers, out var n) is { } text) a[i] = text;
                    count += n;
                }
                break;
        }
        return count;
    }

    /// <summary>
    /// Rewrites the links in every property of <paramref name="item"/> and saves it. It is republished only when it was
    /// published without unpublished edits; otherwise the change stays a draft for an editor to publish.
    /// </summary>
    public static int Apply(IContentService contents, IContent item, Func<string, Guid?> target, ILogger logger, params string[] markers)
    {
        if (markers.Length == 0) markers = OldSite;
        var hadDraft = item.Edited;
        var changed = 0;
        foreach (var property in item.Properties)
        {
            foreach (var pv in property.Values.ToList())
            {
                if (pv.EditedValue is not string raw || !markers.Any(m => raw.Contains(m, StringComparison.OrdinalIgnoreCase))) continue;
                JsonNode? root;
                try { root = JsonNode.Parse(raw); } catch (JsonException) { continue; }
                var n = Rewrite(root, target, markers);
                if (n == 0) continue;
                item.SetValue(property.Alias, root!.ToJsonString(), pv.Culture, pv.Segment);
                changed += n;
            }
        }
        if (changed == 0) return 0;
        ContentChanges.SaveKeepingDrafts(contents, item, hadDraft, logger, $"{changed} links now point to pages of this site");
        return changed;
    }

    /// <summary>Rewrites inside a child; returns the replacement string when the child is JSON text that changed.</summary>
    private static string? Nested(JsonNode? child, Func<string, Guid?> target, string[] markers, out int count)
    {
        if (child is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 1 && s.TrimStart() is ['{' or '[', ..] && markers.Any(m => s.Contains(m, StringComparison.OrdinalIgnoreCase)))
        {
            JsonNode? inner;
            try { inner = JsonNode.Parse(s); } catch (JsonException) { count = 0; return null; }
            count = Rewrite(inner, target, markers);
            return count > 0 ? inner!.ToJsonString() : null;
        }
        count = Rewrite(child, target, markers);
        return null;
    }
}
