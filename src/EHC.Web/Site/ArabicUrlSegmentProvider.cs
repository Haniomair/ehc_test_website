using System.Text.RegularExpressions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace EHC.Web.Site;

/// <summary>
/// URL segments without Arabic diacritics: tashkeel (fathatan, damma, shadda, sukun …), superscript alef and tatweel
/// stay in page names but are dropped from URLs. The default segment builder turns each mark into a hyphen, so
/// "مختبرًا" became "مختبر-ا" and "يكرّم" became "يكر-م". Names without marks are left to the default provider.
/// </summary>
public sealed partial class ArabicUrlSegmentProvider(IShortStringHelper shortStringHelper) : IUrlSegmentProvider
{
    [GeneratedRegex("[ً-ٰٟـ]")]
    private static partial Regex ArabicMarks();

    public static string? StripMarks(string? source) => source is null ? null : ArabicMarks().Replace(source, string.Empty);

    public string? GetUrlSegment(IContentBase content, string? culture = null) => GetUrlSegment(content, published: true, culture);

    public string? GetUrlSegment(IContentBase content, bool published, string? culture = null)
    {
        var source = Source(content, published, culture);
        if (string.IsNullOrWhiteSpace(source) || !ArabicMarks().IsMatch(source)) return null;
        return StripMarks(source)!.ToUrlSegment(shortStringHelper, culture);
    }

    // same source as the default provider: umbracoUrlName when set, otherwise the (published) name
    private static string? Source(IContentBase content, bool published, string? culture)
    {
        var urlName = content.GetValue<string>(Constants.Conventions.Content.UrlName, culture, published: published);
        if (!string.IsNullOrWhiteSpace(urlName)) return urlName;
        if (published && content is IContent c) return c.GetPublishName(culture) ?? c.PublishName;
        return content.GetCultureName(culture) ?? content.Name;
    }
}
