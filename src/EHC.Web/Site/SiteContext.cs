using System.Globalization;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;
using EHC.Web.Themes;

namespace EHC.Web.Site;

public sealed record ContactNumber(string Label, string Number, bool IsEmergency)
{
    /// <summary>Digits and + only, for tel: links.</summary>
    public string Dial => new(Number.Where(c => char.IsDigit(c) || c == '+').ToArray());
}

public sealed record AlternateLink(string Culture, string Language, string Url);

/// <summary>Everything the site chrome (header, menus, footer) needs for one request.</summary>
public sealed class SiteData
{
    public required IPublishedContent Page { get; init; }
    public required IPublishedContent? Home { get; init; }
    public required CultureInfo Culture { get; init; }
    public bool IsRtl => Culture.TextInfo.IsRightToLeft;
    public string Language => Culture.TwoLetterISOLanguageName;
    public required ResolvedTheme Theme { get; init; }
    public required string LogoUrl { get; init; }
    /// <summary>White logo file for dark surfaces, or null when the logo came from a theme / Settings (views then fall
    /// back to a CSS filter). A real file matters: Samsung Internet's "Dark sites" mode renders CSS-filtered images almost black.</summary>
    public string? LogoWhiteUrl { get; init; }
    public required IReadOnlyList<ContactNumber> Phones { get; init; }
    public ContactNumber? Emergency => Phones.FirstOrDefault(p => p.IsEmergency);
    public ContactNumber? Advice => Phones.FirstOrDefault(p => !p.IsEmergency);
    public string? UnifiedNumber { get; init; }
    public string? Email { get; init; }
    public BlockListModel? MegaMenu { get; init; }
    public BlockListModel? FooterColumns { get; init; }
    public IReadOnlyList<Link> UtilityLinks { get; init; } = [];
    public IReadOnlyList<Link> AppLinks { get; init; } = [];
    /// <summary>Site settings › Look: non-default choices (corners, shadows, density, buttons), see Customizer.SiteLooks.</summary>
    public IReadOnlyDictionary<string, string> Looks { get; init; } = new Dictionary<string, string>();
    public Link? HeaderCta { get; init; }
    public Link? NearestLink { get; init; }
    public Link? PrivacyLink { get; init; }
    public bool ShowStagingRibbon { get; init; }
    public required IReadOnlyList<AlternateLink> Alternates { get; init; }
    public AlternateLink? OtherLanguage => Alternates.FirstOrDefault(a => a.Culture != Culture.Name);
}

public interface ISiteContext
{
    SiteData Get(IPublishedContent page);
}

/// <summary>Scoped: resolved once per request and reused by every chrome partial.</summary>
public sealed class SiteContext(IThemeResolver themes) : ISiteContext
{
    private static readonly string[] Cultures = ["ar-SA", "en-US"];
    private SiteData? _data;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public SiteData Get(IPublishedContent page)
    {
        if (_data is not null && _data.Page.Key == page.Key) return _data;

        var culture = CultureInfo.CurrentUICulture;
        var home = page.AncestorOrSelf("home");
        var settings = home?.Value<IPublishedContent>("settings");
        var navigation = settings?.Children().FirstOrDefault(c => c.ContentType.Alias == "navigation");
        var theme = themes.Resolve(page);
        var isAr = culture.TwoLetterISOLanguageName == "ar";

        var customLogo = (isAr ? theme.LogoArUrl : theme.LogoEnUrl)
                         ?? settings?.Value<IPublishedContent>(isAr ? "logoAr" : "logoEn")?.Url();
        var logo = customLogo ?? (isAr ? "/assets/img/logo-ar.png" : "/assets/img/logo-en.png");
        // TODO: white variants for editor/theme logos need their own media fields (content model change)
        var logoWhite = customLogo is null ? (isAr ? "/assets/img/logo-ar-white.png" : "/assets/img/logo-en-white.png") : null;

        var phones = settings?.Value<BlockListModel>("emergencyNumbers")?
            .Select(b => new ContactNumber(b.Content.Value<string>("label") ?? "", b.Content.Value<string>("number") ?? "", b.Content.Value<bool>("isEmergency")))
            .Where(p => !string.IsNullOrWhiteSpace(p.Number))
            .ToList() ?? [];

        var alternates = Cultures
            .Where(c => page.IsPublished(c))
            .Select(c => new AlternateLink(c, c[..2], page.Url(c, UrlMode.Absolute)))
            .Where(a => !string.IsNullOrEmpty(a.Url) && a.Url != "#")
            .ToList();

        return _data = new SiteData
        {
            Page = page,
            Home = home,
            Culture = culture,
            Theme = theme,
            LogoUrl = logo,
            LogoWhiteUrl = logoWhite,
            Phones = phones,
            UnifiedNumber = Clean(settings?.Value<string>("unifiedNumber")),
            Email = Clean(settings?.Value<string>("email")),
            MegaMenu = navigation?.Value<BlockListModel>("megaMenu"),
            FooterColumns = navigation?.Value<BlockListModel>("footerColumns"),
            UtilityLinks = navigation?.Value<IEnumerable<Link>>("utilityLinks")?.ToList() ?? [],
            AppLinks = settings?.Value<IEnumerable<Link>>("sehhatyLinks")?.ToList() ?? [],
            HeaderCta = navigation?.Value<Link>("headerCta"),
            NearestLink = navigation?.Value<Link>("nearestLink"),
            PrivacyLink = navigation?.Value<Link>("privacyLink"),
            ShowStagingRibbon = settings?.Value<bool>("showConceptRibbon") ?? false,
            Looks = Customizer.SiteLooks(a => settings?.Value<string>(a)),
            Alternates = alternates,
        };
    }
}

/// <summary>A mega-menu featured card (campaign / app / map / stats) plus the site data it may need.</summary>
public sealed record MegaFeaturedModel(IPublishedElement Content, SiteData Site);
