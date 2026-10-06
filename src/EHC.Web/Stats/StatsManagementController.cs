using System.Globalization;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using EHC.Web.Feedback;

namespace EHC.Web.Stats;

public sealed record StatsTotals(int Views, int Visits, int Visitors);
public sealed record StatsDay(DateOnly Day, int Views, int Visits, int Visitors);
/// <summary>One row of a "top" list. Label and Url are set for pages.</summary>
public sealed record StatsItem(string Value, int Views, int Visits, int Visitors, string? Label = null, string? Url = null);
public sealed record StatsReport(
    DateOnly From,
    DateOnly To,
    DateOnly Today,
    StatsTotals Totals,
    StatsTotals Previous,
    IReadOnlyList<StatsDay> Days,
    IReadOnlyDictionary<string, IReadOnlyList<StatsItem>> Top,
    DateOnly? GeoBuilt);

public sealed record LiveReport(
    DateTimeOffset At,
    int Active,
    IReadOnlyList<int> PerMinute,
    IReadOnlyList<LivePlace> Places,
    IReadOnlyList<LiveItem> Countries,
    IReadOnlyList<LiveItem> Pages,
    IReadOnlyList<LiveItem> Sources,
    IReadOnlyList<LiveItem> Devices,
    bool GeoLoaded);

/// <summary>
/// Backoffice API for the "Statistics" section (users whose group has that section):
/// /umbraco/management/api/v1/ehc/stats/{summary|export}?from=yyyy-MM-dd&amp;to=yyyy-MM-dd and .../live.
/// </summary>
[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("ehc/stats")]
[ApiExplorerSettings(GroupName = "EHC")]
[Authorize(Policy = StatsSetup.Policy)]
public sealed class StatsManagementController(
    StatsReports reports,
    StatsLive live,
    StatsCalendar calendar,
    TimeProvider clock,
    IStatsGeo geo,
    IUmbracoContextFactory contexts) : ManagementApiControllerBase
{
    public const int MaxDays = 400;

    /// <summary>Default: the last 30 days. Never past today, never longer than <see cref="MaxDays"/>.</summary>
    public static (DateOnly From, DateOnly To) Range(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var end = to is { } t && t < today ? t : today;
        var start = from ?? end.AddDays(-29);
        if (start > end) start = end;
        if (end.DayNumber - start.DayNumber >= MaxDays) start = end.AddDays(-(MaxDays - 1));
        return (start, end);
    }

    [HttpGet("summary")]
    public ActionResult<StatsReport> Summary(DateOnly? from = null, DateOnly? to = null, int top = 25)
    {
        var today = calendar.Today;
        var (start, end) = Range(from, to, today);
        var length = end.DayNumber - start.DayNumber + 1;
        var current = reports.Load(start, end);
        var previous = reports.Load(start.AddDays(-length), start.AddDays(-1), totalOnly: true);
        top = Math.Clamp(top, 5, 100);

        using var cref = contexts.EnsureUmbracoContext();
        var lists = current.Rows
            .Where(r => r.Dimension != StatsStore.Total)
            .GroupBy(r => r.Dimension)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<StatsItem>)g.OrderByDescending(r => r.Views).ThenByDescending(r => r.Visitors).ThenBy(r => r.Value, StringComparer.Ordinal)
                    .Take(top).Select(r => Item(cref.UmbracoContext, r)).ToList());

        return Ok(new StatsReport(
            start, end, today,
            Totals(current.Total), Totals(previous.Total),
            current.Days.Select(d => new StatsDay(d.Day, d.Views, d.Visits, d.Visitors)).ToList(),
            lists,
            geo.Built is { } built ? DateOnly.FromDateTime(built) : null));
    }

    /// <summary>Visitors on the site now (last 5 minutes), page views per minute and arrivals in the last 30 minutes.</summary>
    [HttpGet("live")]
    public ActionResult<LiveReport> Live()
    {
        Response.Headers.CacheControl = "no-store";
        var s = live.Snapshot();
        using var cref = contexts.EnsureUmbracoContext();
        var pages = s.Pages.Select(p =>
        {
            var item = Item(cref.UmbracoContext, new StatsRow(StatsStore.Page, p.Value, p.Count, 0, 0));
            return p with { Label = item.Label, Url = item.Url };
        }).ToList();
        return Ok(new LiveReport(clock.GetUtcNow(), s.Active, s.PerMinute, s.Places, s.Countries, pages, s.Sources, s.Devices, geo.Built is not null));
    }

    [HttpGet("export")]
    public IActionResult Export(DateOnly? from = null, DateOnly? to = null)
    {
        var (start, end) = Range(from, to, calendar.Today);
        var range = reports.Load(start, end);
        using var cref = contexts.EnsureUmbracoContext();
        var csv = new StringBuilder("dimension,value,label,views,visits,visitors\r\n");
        foreach (var d in range.Days)
        {
            csv.Append(string.Join(',', "day", d.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "", d.Views, d.Visits, d.Visitors)).Append("\r\n");
        }
        foreach (var r in range.Rows.OrderBy(r => r.Dimension, StringComparer.Ordinal).ThenByDescending(r => r.Views))
        {
            var item = Item(cref.UmbracoContext, r);
            csv.Append(string.Join(',', r.Dimension, FeedbackManagementController.Cell(r.Value), FeedbackManagementController.Cell(item.Label), r.Views, r.Visits, r.Visitors)).Append("\r\n");
        }
        // BOM so Excel opens Arabic text correctly
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv; charset=utf-8", $"visitor-statistics-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
    }

    private static StatsTotals Totals(StatsRow r) => new(r.Views, r.Visits, r.Visitors);

    private static StatsItem Item(IUmbracoContext ctx, StatsRow r)
    {
        if (r.Dimension != StatsStore.Page) return new StatsItem(r.Value, r.Views, r.Visits, r.Visitors);
        var parts = r.Value.Split('|');
        var page = Guid.TryParse(parts[0], out var key) ? ctx.Content?.GetById(key) : null;
        var culture = parts.Length > 1 ? parts[1] : null;
        return page is null
            ? new StatsItem(r.Value, r.Views, r.Visits, r.Visitors, "(deleted page)")
            : new StatsItem(r.Value, r.Views, r.Visits, r.Visitors, page.Name(culture) ?? page.Name, page.Url(culture, UrlMode.Relative));
    }
}
