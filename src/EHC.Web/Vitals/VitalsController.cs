using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;

namespace EHC.Web.Vitals;

public sealed record VitalsInput(Guid PageKey, string? Culture, string? Device, Dictionary<string, double>? Metrics);

/// <summary>
/// POST /api/vitals — real-visitor Core Web Vitals from components/vitals.js (sent only after consent to optional
/// cookies). JSON only, rate limited per IP (the address is used in memory for the limit, never stored), page must be
/// published, metric names and value ranges checked. Nothing is stored that identifies a visitor.
/// </summary>
[ApiController]
[Route("api/vitals")]
[EnableRateLimiting(Api.ApiSetup.VitalsPolicy)]
public sealed class VitalsController(IVitalsStore store, IPublishedContentQuery content, IOptions<VitalsOptions> options, TimeProvider clock) : ControllerBase
{
    private static readonly string[] Cultures = ["ar-SA", "en-US"];

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Post([FromBody] VitalsInput input)
    {
        Response.Headers.CacheControl = "no-store";
        if (!options.Value.Enabled) return NoContent();
        if (input.PageKey == Guid.Empty || content.Content(input.PageKey) is null) return BadRequest();
        var culture = Cultures.FirstOrDefault(c => string.Equals(c, input.Culture, StringComparison.OrdinalIgnoreCase));
        var device = VitalsMetrics.Devices.FirstOrDefault(d => d == input.Device);
        if (culture is null || device is null || input.Metrics is null) return BadRequest();

        var now = clock.GetUtcNow().UtcDateTime;
        var rows = input.Metrics
            .Where(m => VitalsMetrics.Valid(m.Key, m.Value))
            .Select(m => new VitalDto { PageKey = input.PageKey, Culture = culture, Device = device, Metric = m.Key, Value = m.Value, CreatedUtc = now })
            .ToList();
        if (rows.Count == 0) return BadRequest();
        await store.AddAsync(rows);
        return NoContent();
    }
}
