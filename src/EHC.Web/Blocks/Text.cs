using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace EHC.Web.Blocks;

public static partial class Text
{
    [GeneratedRegex(@"\*([^*\r\n]+)\*")]
    private static partial Regex Starred();

    /// <summary>
    /// Plain text with *starred* words shown as the highlighted phrase (&lt;em&gt;). Everything is HTML-encoded first,
    /// so editors can't inject markup; only the asterisk pairs become tags.
    /// </summary>
    public static IHtmlContent Emphasis(string? text, HtmlEncoder? encoder = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return HtmlString.Empty;
        var encoded = (encoder ?? HtmlEncoder.Default).Encode(text.Trim());
        return new HtmlString(Starred().Replace(encoded, "<em>$1</em>"));
    }

    /// <summary>The same text without the asterisks, e.g. for aria-labels.</summary>
    public static string Plain(string? text) => string.IsNullOrWhiteSpace(text) ? "" : text.Replace("*", "").Trim();
}

/// <summary>Eyebrow + heading + intro shared by most sections (Views/Partials/blocklist/_SectionHeader.cshtml).</summary>
public sealed record SectionHeader(string? Eyebrow, string? Heading, string? Intro, bool OnDark = false, string? Id = null);
