using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace EHC.Web.Api;

public sealed record SearchHit(string Title, string Url, string? Icon);
public sealed record SearchGroup(string Key, IReadOnlyList<SearchHit> Items);

/// <summary>
/// GET /api/search?q=&amp;culture= — Examine external index (published content only), grouped by kind, max 8 per group.
/// Arabic normalisation (hamza, taa marbuta) is a later improvement (docs/06-integrations.md).
/// </summary>
[ApiController]
[Route("api/search")]
[EnableRateLimiting(ApiSetup.Policy)]
public sealed class SearchController(IPublishedContentQuery content) : ControllerBase
{
    private const int PerGroup = 8;

    // content type → group key (order = display order); anything else routable falls into "pages"
    private static readonly (string Group, string[] Types)[] Groups =
    [
        ("specialties", ["specialty"]),
        ("doctors", ["doctor"]),
        ("facilities", ["facility"]),
        ("campaigns", ["campaign"]),
        ("news", ["newsItem"]),
        ("pages", ["landingPage", "contentPage", "home", "doctorFolder", "facilityFolder", "newsFolder", "campaignFolder", "specialtyFolder"]),
    ];

    [HttpGet]
    [OutputCache(Duration = 60, VaryByQueryKeys = ["q", "culture"])]
    public ActionResult<IEnumerable<SearchGroup>> Get([FromQuery] string? q, [FromQuery] string? culture)
    {
        var term = q?.Trim() ?? "";
        if (term.Length < 2) return Ok(Array.Empty<SearchGroup>());
        if (term.Length > 100) term = term[..100];
        var c = ApiCulture.From(culture);

        var hits = content.Search(term, c)
            .Select(r => r.Content)
            .Where(x => x is not null && x.IsPublished(c) && x.Value<bool>("noIndex") is false)
            .ToList();

        var groups = Groups
            .Select(g => new SearchGroup(g.Group, hits
                .Where(h => g.Types.Contains(h.ContentType.Alias))
                .Take(PerGroup)
                .Select(h => new SearchHit(Title(h, c), h.Url(c, UrlMode.Relative), Icon(h)))
                .ToList()))
            .Where(g => g.Items.Count > 0)
            .ToList();
        return Ok(groups);
    }

    private static string Title(IPublishedContent x, string culture)
    {
        var t = x.ContentType.Alias == "doctor" ? x.Value<string>("fullName", culture) : x.Value<string>("pageTitle", culture);
        return string.IsNullOrWhiteSpace(t) ? x.Name(culture) ?? x.Name : t;
    }

    private static string? Icon(IPublishedContent x) => x.ContentType.Alias switch
    {
        "specialty" => x.Value<string>("icon") ?? "heart",
        "doctor" => "doc",
        "facility" => x.Value<bool>("hasEmergency") ? "er" : "hosp",
        "campaign" => "flag",
        "newsItem" => "news",
        _ => "arrow",
    };
}
