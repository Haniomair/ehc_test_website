using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;

namespace EHC.Web.Stats;

public sealed record StatsInput(Guid PageKey, string? Culture, string? Referrer, string? Source, string? Campaign, bool Touch);
public sealed record HeatClickInput(string? S, int X, int Y);
/// <summary>Depth is null on later reports from the same page view (only new clicks are sent then).</summary>
public sealed record HeatInput(Guid PageKey, string? Culture, string? Device, int? Depth, IReadOnlyList<HeatClickInput>? Clicks);

/// <summary>
/// POST /api/stats — one page view from components/stats.js. JSON only, same-origin, rate limited per IP, page must be
/// published, automated clients ignored. The IP address and user-agent string are used in memory to derive the daily
/// visitor hash, place and device groups, and are never stored (see StatsHitDto).
/// POST /api/stats/heat — clicks and scroll depth from components/heat.js (visitors who accepted optional cookies).
/// </summary>
[ApiController]
[Route("api/stats")]
[EnableRateLimiting(Api.ApiSetup.StatsPolicy)]
public sealed class StatsController(
    StatsQueue queue,
    StatsLive live,
    IStatsHeat heat,
    StatsVisitors visitors,
    IStatsGeo geo,
    IPublishedContentQuery content,
    IOptions<StatsOptions> options,
    TimeProvider clock) : ControllerBase
{
    private static readonly string[] Cultures = ["ar-SA", "en-US"];

    /// <summary>Browsers send Origin with every POST; another site's page must not add data.</summary>
    private bool ForeignOrigin() =>
        Request.Headers.Origin is { Count: > 0 } origin
        && (!Uri.TryCreate(origin.ToString(), UriKind.Absolute, out var from) || !string.Equals(from.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase));

    [HttpPost]
    [Consumes("application/json")]
    public IActionResult Post([FromBody] StatsInput input)
    {
        Response.Headers.CacheControl = "no-store";
        if (!options.Value.Enabled) return NoContent();
        if (ForeignOrigin()) return StatusCode(StatusCodes.Status403Forbidden);
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

        var now = clock.GetUtcNow().UtcDateTime;
        var device = StatsTraffic.Device(userAgent, input.Touch);
        live.Add(new LiveHit(now, visitor, newVisit, input.PageKey, culture, place, source?.Medium, source?.Source, device));
        queue.TryAdd(new StatsHitDto
        {
            CreatedUtc = now,
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
            Device = device,
            Browser = StatsTraffic.Browser(userAgent),
            Os = StatsTraffic.OperatingSystem(userAgent, input.Touch),
        });
        return NoContent();
    }

    [HttpPost("heat")]
    [Consumes("application/json")]
    public IActionResult Heat([FromBody] HeatInput input)
    {
        Response.Headers.CacheControl = "no-store";
        if (!options.Value.Enabled || !options.Value.Heatmaps) return NoContent();
        if (ForeignOrigin()) return StatusCode(StatusCodes.Status403Forbidden);
        if (StatsTraffic.IsBot(Request.Headers.UserAgent.ToString())) return NoContent();
        var culture = Cultures.FirstOrDefault(c => string.Equals(c, input.Culture, StringComparison.OrdinalIgnoreCase));
        var device = StatsTraffic.Devices.FirstOrDefault(d => d == input.Device);
        var clicks = input.Clicks ?? [];
        if (culture is null || device is null || input.PageKey == Guid.Empty || clicks.Count > HeatRules.MaxClicksPerView
            || input.Depth is < 0 or > 100 || content.Content(input.PageKey) is null)
        {
            return BadRequest();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var rows = clicks
            .Where(c => HeatRules.ValidSelector(c.S) && c.X is >= 0 and <= 100 && c.Y is >= 0 and <= 100)
            .Select(c => new StatsClickDto { CreatedUtc = now, PageKey = input.PageKey, Culture = culture, Device = device, Selector = c.S!, X = c.X, Y = c.Y })
            .ToList();
        var scroll = input.Depth is { } depth
            ? new StatsScrollDto { CreatedUtc = now, PageKey = input.PageKey, Culture = culture, Device = device, Depth = depth }
            : null;
        if (scroll is null && rows.Count == 0) return NoContent();
        heat.Add(scroll, rows);
        return NoContent();
    }
}
