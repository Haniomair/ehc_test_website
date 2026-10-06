using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;

namespace EHC.Web.Stats;

public sealed record StatsInput(Guid PageKey, string? Culture, string? Referrer, string? Source, string? Campaign, bool Touch);

/// <summary>
/// POST /api/stats — one page view from components/stats.js. JSON only, same-origin, rate limited per IP, page must be
/// published, automated clients ignored. The IP address and user-agent string are used in memory to derive the daily
/// visitor hash, place and device groups, and are never stored (see StatsHitDto).
/// </summary>
[ApiController]
[Route("api/stats")]
[EnableRateLimiting(Api.ApiSetup.StatsPolicy)]
public sealed class StatsController(
    StatsQueue queue,
    StatsVisitors visitors,
    IStatsGeo geo,
    IPublishedContentQuery content,
    IOptions<StatsOptions> options,
    TimeProvider clock) : ControllerBase
{
    private static readonly string[] Cultures = ["ar-SA", "en-US"];

    [HttpPost]
    [Consumes("application/json")]
    public IActionResult Post([FromBody] StatsInput input)
    {
        Response.Headers.CacheControl = "no-store";
        if (!options.Value.Enabled) return NoContent();
        // browsers send Origin with every POST; another site's page must not add views
        if (Request.Headers.Origin is { Count: > 0 } origin
            && (!Uri.TryCreate(origin.ToString(), UriKind.Absolute, out var from) || !string.Equals(from.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
        var userAgent = Request.Headers.UserAgent.ToString();
        if (StatsTraffic.IsBot(userAgent)) return NoContent();
        if (input.PageKey == Guid.Empty || content.Content(input.PageKey) is null) return BadRequest();
        var culture = Cultures.FirstOrDefault(c => string.Equals(c, input.Culture, StringComparison.OrdinalIgnoreCase));
        if (culture is null) return BadRequest();

        var address = HttpContext.Connection.RemoteIpAddress;
        if (address is { IsIPv4MappedToIPv6: true }) address = address.MapToIPv4();
        var (visitor, newVisit) = visitors.Identify(address?.ToString() ?? "", userAgent);
        var place = geo.Lookup(address);
        var source = newVisit ? StatsTraffic.Source(input.Referrer, Request.Host.Host, input.Source) : (TrafficSource?)null;

        queue.TryAdd(new StatsHitDto
        {
            CreatedUtc = clock.GetUtcNow().UtcDateTime,
            PageKey = input.PageKey,
            Culture = culture,
            Visitor = visitor,
            NewVisit = newVisit,
            Medium = source?.Medium,
            Source = source?.Source,
            Campaign = source?.Medium == StatsTraffic.Campaign ? StatsTraffic.Clean(input.Campaign, 100) : null,
            Country = place.Country,
            Region = place.Region,
            City = place.City,
            Device = StatsTraffic.Device(userAgent, input.Touch),
            Browser = StatsTraffic.Browser(userAgent),
            Os = StatsTraffic.OperatingSystem(userAgent, input.Touch),
        });
        return NoContent();
    }
}
