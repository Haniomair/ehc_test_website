using System.Net;
using System.Text.RegularExpressions;

namespace EHC.Web.Blocks;

/// <summary>
/// "On this page" for a long rich text: every h2 gets an id (an existing one is kept) and is listed in order.
/// The body comes from the restricted editor; only h2 tags are touched, the rest is passed through unchanged.
/// </summary>
public static partial class RichTextToc
{
    public sealed record Entry(string Id, string Text);

    public sealed record Result(string Html, IReadOnlyList<Entry> Entries);

    [GeneratedRegex(@"<h2(?<attrs>(?:\s[^>]*)?)>(?<inner>.*?)</h2\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex H2();

    [GeneratedRegex(@"\sid\s*=\s*""(?<id>[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex IdAttr();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonWord();

    public static Result From(string html)
    {
        var entries = new List<Entry>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var result = H2().Replace(html, m =>
        {
            var text = WebUtility.HtmlDecode(Tags().Replace(m.Groups["inner"].Value, "")).Trim();
            if (text.Length == 0) return m.Value;
            var attrs = m.Groups["attrs"].Value;
            if (IdAttr().Match(attrs) is { Success: true } existing)
            {
                var own = WebUtility.HtmlDecode(existing.Groups["id"].Value);
                used.Add(own);
                entries.Add(new(own, text));
                return m.Value;
            }
            var id = Unique(Slug(text), used);
            entries.Add(new(id, text));
            return $"<h2 id=\"{id}\"{attrs}>{m.Groups["inner"].Value}</h2>";
        });
        return new(result, entries);
    }

    /// <summary>"sec-" + lower-case letters and digits (any script) joined by dashes; safe in an attribute and a URL fragment.</summary>
    private static string Slug(string text)
    {
        var s = NonWord().Replace(text.ToLowerInvariant(), "-").Trim('-');
        if (s.Length > 40) s = s[..40].TrimEnd('-');
        return "sec-" + (s.Length > 0 ? s : "x");
    }

    private static string Unique(string id, HashSet<string> used)
    {
        var candidate = id;
        for (var n = 2; !used.Add(candidate); n++) candidate = $"{id}-{n}";
        return candidate;
    }
}
