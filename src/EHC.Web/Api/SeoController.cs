using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace EHC.Web.Api;

/// <summary>/sitemap.xml (every published, indexable page in each language, with hreflang alternates) and /robots.txt.</summary>
[ApiController]
public sealed class SeoController(IPublishedContentQuery content) : ControllerBase
{
    private static readonly string[] Cultures = ["ar-SA", "en-US"];
    private static readonly XNamespace Sm = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly XNamespace Xhtml = "http://www.w3.org/1999/xhtml";

    /// <summary>Internal route; /sitemap.xml is rewritten to it (see EhcComposer).</summary>
    public const string SitemapPath = "/ehc-sitemap";

    [HttpGet(SitemapPath)]
    [OutputCache(Duration = 600)]
    public IActionResult Sitemap()
    {
        var pages = content.ContentAtRoot()
            .Where(r => r.ContentType.Alias == "home")
            .SelectMany(h => new[] { h }.Concat(h.Descendants()))
            .Where(p => p.TemplateId > 0 && !p.Value<bool>("noIndex"))
            .ToList();

        var urlset = new XElement(Sm + "urlset", new XAttribute(XNamespace.Xmlns + "xhtml", Xhtml));
        foreach (var page in pages)
        {
            var urls = Cultures
                .Where(c => page.IsPublished(c))
                .Select(c => (Culture: c, Url: page.Url(c, UrlMode.Absolute)))
                .Where(x => !string.IsNullOrEmpty(x.Url) && x.Url != "#")
                .ToList();
            foreach (var (_, url) in urls)
            {
                var entry = new XElement(Sm + "url",
                    new XElement(Sm + "loc", url),
                    new XElement(Sm + "lastmod", page.UpdateDate.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                foreach (var alt in urls)
                {
                    entry.Add(new XElement(Xhtml + "link", new XAttribute("rel", "alternate"), new XAttribute("hreflang", alt.Culture[..2]), new XAttribute("href", alt.Url)));
                }
                urlset.Add(entry);
            }
        }

        var xml = new XDocument(new XDeclaration("1.0", "utf-8", null), urlset);
        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(new StringWriterUtf8(sb), new XmlWriterSettings { Indent = true }))
        {
            xml.Save(writer);
        }
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    [HttpGet("/robots.txt")]
    [OutputCache(Duration = 3600)]
    public IActionResult Robots()
    {
        var origin = $"{Request.Scheme}://{Request.Host}";
        var text = string.Join('\n',
            "User-agent: *",
            "Disallow: /umbraco/",
            "Disallow: /api/",
            "",
            $"Sitemap: {origin}/sitemap.xml",
            "");
        return Content(text, "text/plain", Encoding.UTF8);
    }

    private sealed class StringWriterUtf8(StringBuilder sb) : StringWriter(sb, CultureInfo.InvariantCulture)
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
