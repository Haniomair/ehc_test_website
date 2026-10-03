using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;

namespace EHC.Web.Vitals;

public sealed record VitalSummary(string Metric, string Device, int Samples, double P75, int GoodPercent, string Rating);
public sealed record PageVitals(Guid PageKey, string Name, string? Url, int Samples, IReadOnlyDictionary<string, VitalSummary?> Metrics);
public sealed record VitalsReport(int Days, IReadOnlyList<VitalSummary> Overall, IReadOnlyList<PageVitals> Pages);

/// <summary>
/// Backoffice API for the "Core Web Vitals" dashboard (Content section users only):
/// /umbraco/management/api/v1/ehc/vitals/summary?days=28&amp;device=mobile.
/// </summary>
[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("ehc/vitals")]
[ApiExplorerSettings(GroupName = "EHC")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
public sealed class VitalsManagementController(IVitalsStore store, IUmbracoContextFactory contexts, TimeProvider clock) : ManagementApiControllerBase
{
    [HttpGet("summary")]
    public ActionResult<VitalsReport> Summary(int days = 28, string device = "mobile", int pages = 25)
    {
        days = Math.Clamp(days, 1, 400);
        if (!VitalsMetrics.Devices.Contains(device)) device = "mobile";
        var since = clock.GetUtcNow().UtcDateTime.AddDays(-days);

        var overall = new List<VitalSummary>();
        foreach (var d in VitalsMetrics.Devices)
            foreach (var m in VitalsMetrics.Names)
                if (store.Stat(since, m, d) is { } s) overall.Add(Summary(m, d, s));

        using var cref = contexts.EnsureUmbracoContext();
        var rows = store.BusiestPages(since, device, pages).Select(p =>
        {
            var page = cref.UmbracoContext.Content?.GetById(p.PageKey);
            var metrics = VitalsMetrics.Names.ToDictionary(m => m, m => store.Stat(since, m, device, p.PageKey) is { } s ? Summary(m, device, s) : null);
            return new PageVitals(p.PageKey, page?.Name ?? "(deleted page)", page?.Url(), p.Samples, metrics);
        }).ToList();
        return Ok(new VitalsReport(days, overall, rows));
    }

    private static VitalSummary Summary(string metric, string device, VitalStat s) =>
        new(metric, device, s.Samples, Math.Round(s.P75, metric == "CLS" ? 3 : 0), s.GoodPercent, VitalsMetrics.Rating(metric, s.P75));
}
