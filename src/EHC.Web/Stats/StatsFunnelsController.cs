using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace EHC.Web.Stats;

public sealed record FunnelInput(string? Name, IReadOnlyList<Guid>? Steps);
public sealed record FunnelStep(Guid Key, string Name, string? Url);
public sealed record FunnelView(int Id, string Name, DateOnly Since, IReadOnlyList<FunnelStep> Steps);
public sealed record FunnelStepResult(Guid Key, string Name, string? Url, int Visitors);
public sealed record FunnelReport(int Id, string Name, DateOnly Since, DateOnly From, DateOnly To, IReadOnlyList<FunnelStepResult> Steps);

/// <summary>
/// Backoffice API for the "Funnels" dashboard (users whose group has the Statistics section):
/// /umbraco/management/api/v1/ehc/stats/funnels[/{id}[/report?from=yyyy-MM-dd&amp;to=yyyy-MM-dd]].
/// </summary>
[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("ehc/stats/funnels")]
[ApiExplorerSettings(GroupName = "EHC")]
[Authorize(Policy = StatsSetup.Policy)]
public sealed class StatsFunnelsController(IStatsFunnels funnels, StatsCalendar calendar, IUmbracoContextFactory contexts) : ManagementApiControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<FunnelView>> GetFunnels()
    {
        using var cref = contexts.EnsureUmbracoContext();
        return Ok(funnels.All().Select(f => View(cref.UmbracoContext, f)).ToList());
    }

    [HttpPost]
    public IActionResult CreateFunnel([FromBody] FunnelInput input) => SaveFunnel(null, input);

    [HttpPut("{id:int}")]
    public IActionResult UpdateFunnel(int id, [FromBody] FunnelInput input) =>
        funnels.Get(id) is null ? NotFound() : SaveFunnel(id, input);

    [HttpDelete("{id:int}")]
    public IActionResult DeleteFunnel(int id) => funnels.Delete(id) ? NoContent() : NotFound();

    [HttpGet("{id:int}/report")]
    public ActionResult<FunnelReport> GetFunnelReport(int id, DateOnly? from = null, DateOnly? to = null)
    {
        if (funnels.Get(id) is not { } funnel) return NotFound();
        var (start, end) = StatsManagementController.Range(from, to, calendar.Today);
        var counts = funnels.Report(funnel, start, end);
        using var cref = contexts.EnsureUmbracoContext();
        var steps = View(cref.UmbracoContext, funnel).Steps.Select((s, i) => new FunnelStepResult(s.Key, s.Name, s.Url, counts[i])).ToList();
        return Ok(new FunnelReport(funnel.Id, funnel.Name, funnel.Since, start, end, steps));
    }

    private IActionResult SaveFunnel(int? id, FunnelInput input)
    {
        var steps = input.Steps ?? [];
        var problem = FunnelMath.Validate(input.Name, steps);
        if (problem is null)
        {
            using var cref = contexts.EnsureUmbracoContext();
            if (steps.Any(s => cref.UmbracoContext.Content?.GetById(s) is null)) problem = "Every step must be a published page.";
        }
        if (problem is not null) return BadRequest(new ProblemDetails { Title = problem, Status = 400 });

        var saved = funnels.Save(id, input.Name!, steps);
        using var ctx = contexts.EnsureUmbracoContext();
        return Ok(View(ctx.UmbracoContext, saved));
    }

    private static FunnelView View(IUmbracoContext ctx, StatsFunnel f) => new(
        f.Id, f.Name, f.Since,
        f.Steps.Select(key => ctx.Content?.GetById(key) is { } page
            ? new FunnelStep(key, page.Name, page.Url(mode: UrlMode.Relative))
            : new FunnelStep(key, "(deleted page)", null)).ToList());
}
