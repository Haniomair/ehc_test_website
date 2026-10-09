using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace EHC.Web.Api;

/// <summary>A facility for maps and finders. ErFeedId: the emergency department's id in /api/er-wait (live wait times), when it has one.</summary>
public sealed record FacilityDto(Guid Id, string Name, string? City, string Type, bool HasEmergency, decimal? Lat, decimal? Lng, string Url, string? ErFeedId = null);

/// <summary>GET /api/facilities?type=&amp;culture= — published facility nodes for maps and the facility finder.</summary>
[ApiController]
[Route("api/facilities")]
[EnableRateLimiting(ApiSetup.Policy)]
public sealed class FacilitiesController(IPublishedContentQuery content) : ControllerBase
{
    private static readonly HashSet<string> Types = ["hospital", "primaryCare", "specialist", "emergency"];

    [HttpGet]
    [OutputCache(Duration = 60, VaryByQueryKeys = ["type", "culture"])]
    public ActionResult<IEnumerable<FacilityDto>> Get([FromQuery] string? type, [FromQuery] string? culture)
    {
        var c = ApiCulture.From(culture);
        if (type is not null && !Types.Contains(type)) return BadRequest();

        var result = content.ContentAtRoot()
            .Where(r => r.ContentType.Alias == "home")
            .SelectMany(h => h.DescendantsOfType("facility", c))
            .Where(f => f.IsPublished(c))
            .Where(f => type is null || (type == "emergency" ? f.Value<bool>("hasEmergency") : f.Value<string>("facilityType") == type))
            .Select(f => new FacilityDto(
                f.Key,
                f.Name(c) ?? f.Name,
                f.Value<string>("city", c),
                f.Value<string>("facilityType") ?? "hospital",
                f.Value<bool>("hasEmergency"),
                f.Value<decimal?>("latitude"),
                f.Value<decimal?>("longitude"),
                f.Url(c, UrlMode.Relative),
                f.Value<bool>("hasEmergency") && f.Value<string>("erFeedId") is { Length: > 0 } feed ? feed : null))
            .OrderBy(f => f.Name)
            .ToList();
        return Ok(result);
    }
}
