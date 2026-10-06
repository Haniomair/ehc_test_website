using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace EHC.Web.Stats;

public sealed record HeatPageItem(string Value, string Label, string? Url, int Views, int Clicks);
public sealed record HeatmapReport(string Page, string Label, string? Url, string Device, DateOnly From, DateOnly To, int Views, IReadOnlyList<int> Reach, IReadOnlyList<HeatClick> Clicks);

/// <summary>
/// Backoffice API for the "Heatmaps" dashboard (users whose group has the Statistics section):
/// /umbraco/management/api/v1/ehc/stats/heatmap/pages?from&amp;to and .../heatmap?page={key}|{culture}&amp;device=mobile&amp;from&amp;to.
/// </summary>
[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("ehc/stats/heatmap")]
[ApiExplorerSettings(GroupName = "EHC")]
[Authorize(Policy = StatsSetup.Policy)]
public sealed class StatsHeatmapController(IStatsHeat heat, StatsCalendar calendar, IUmbracoContextFactory contexts) : ManagementApiControllerBase
{
    private const int MaxClickGroups = 5000;

    [HttpGet("pages")]
    public ActionResult<IEnumerable<HeatPageItem>> GetHeatmapPages(DateOnly? from = null, DateOnly? to = null)
    {
        var (start, end) = StatsManagementController.Range(from, to, calendar.Today);
        using var cref = contexts.EnsureUmbracoContext();
        return Ok(heat.Pages(calendar.StartUtc(start), calendar.StartUtc(end.AddDays(1)), 100)
            .Select(p =>
            {
                var (label, url) = Page(cref.UmbracoContext, p.PageKey, p.Culture);
                return new HeatPageItem(StatsStore.PageValue(p.PageKey, p.Culture), label, url, p.Views, p.Clicks);
            })
            .ToList());
    }

    [HttpGet]
    public ActionResult<HeatmapReport> GetHeatmap(string page, string device = "desktop", DateOnly? from = null, DateOnly? to = null)
    {
        var parts = (page ?? "").Split('|');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var key) || !StatsTraffic.Devices.Contains(device)) return BadRequest();
        var culture = parts[1];
        var (start, end) = StatsManagementController.Range(from, to, calendar.Today);
        DateTime fromUtc = calendar.StartUtc(start), toUtc = calendar.StartUtc(end.AddDays(1));

        var depths = heat.Depths(key, culture, device, fromUtc, toUtc);
        var clicks = heat.Clicks(key, culture, device, fromUtc, toUtc, MaxClickGroups);
        using var cref = contexts.EnsureUmbracoContext();
        var (label, url) = Page(cref.UmbracoContext, key, culture);
        return Ok(new HeatmapReport(page!, label, url, device, start, end, depths.Values.Sum(), HeatRules.Reach(depths), clicks));
    }

    private static (string Label, string? Url) Page(IUmbracoContext ctx, Guid key, string culture) =>
        ctx.Content?.GetById(key) is { } p ? (p.Name(culture) ?? p.Name, p.Url(culture, UrlMode.Relative)) : ("(deleted page)", null);
}
