using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace EHC.Web.Blocks;

public static class HtmlAttributes
{
    /// <summary>
    /// Renders ` data-{name}="{value}"` (HTML-encoded), or nothing when the value is empty.
    /// Razor keeps empty data-* attributes, unlike other attributes, so optional ones go through here.
    /// </summary>
    public static IHtmlContent Data(string name, string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? HtmlString.Empty
            : new HtmlString($" data-{name}=\"{HtmlEncoder.Default.Encode(value)}\"");
}
