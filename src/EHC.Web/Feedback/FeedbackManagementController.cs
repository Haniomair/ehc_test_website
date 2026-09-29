using System.Globalization;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;

namespace EHC.Web.Feedback;

public sealed record PageFeedback(Guid PageKey, string Name, string? Url, int Yes, int No, int Percent, IReadOnlyDictionary<string, int> Reasons);
public sealed record FeedbackComment(Guid PageKey, string Page, string Culture, string? Reason, string Comment, DateTime CreatedUtc);

/// <summary>
/// Backoffice API for the "Page feedback" dashboard (Content section users only):
/// /umbraco/management/api/v1/ehc/feedback/{summary|comments|export}.
/// </summary>
[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("ehc/feedback")]
[ApiExplorerSettings(GroupName = "EHC")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
public sealed class FeedbackManagementController(IFeedbackStore store, IUmbracoContextFactory contexts, TimeProvider clock) : ManagementApiControllerBase
{
    private DateTime Since(int days) => clock.GetUtcNow().UtcDateTime.AddDays(-Math.Clamp(days, 1, 400));

    private (string Name, string? Url) Page(IUmbracoContext ctx, Guid key)
    {
        var page = ctx.Content?.GetById(key);
        return page is null ? ("(deleted page)", null) : (page.Name ?? key.ToString(), page.Url());
    }

    [HttpGet("summary")]
    public ActionResult<IEnumerable<PageFeedback>> Summary(int days = 90)
    {
        var since = Since(days);
        var reasons = store.Reasons(since).GroupBy(r => r.PageKey).ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, int>)g.ToDictionary(r => r.Reason, r => r.Count));
        using var cref = contexts.EnsureUmbracoContext();
        var rows = store.Scores(since).Select(s =>
        {
            var (name, url) = Page(cref.UmbracoContext, s.PageKey);
            var total = s.Yes + s.No;
            return new PageFeedback(s.PageKey, name, url, s.Yes, s.No, total == 0 ? 0 : (int)Math.Round(100.0 * s.Yes / total),
                reasons.GetValueOrDefault(s.PageKey) ?? new Dictionary<string, int>());
        });
        // least helpful first: those need attention
        return Ok(rows.OrderBy(r => r.Percent).ThenByDescending(r => r.No).ToList());
    }

    [HttpGet("comments")]
    public ActionResult<IEnumerable<FeedbackComment>> Comments(int days = 90, Guid? pageKey = null, int take = 100)
    {
        using var cref = contexts.EnsureUmbracoContext();
        return Ok(store.Comments(Since(days), pageKey, take)
            .Select(c => new FeedbackComment(c.PageKey, Page(cref.UmbracoContext, c.PageKey).Name, c.Culture, c.Reason, c.Comment ?? "", c.CreatedUtc))
            .ToList());
    }

    [HttpGet("export")]
    public IActionResult Export(int days = 365)
    {
        using var cref = contexts.EnsureUmbracoContext();
        var names = new Dictionary<Guid, string>();
        var csv = new StringBuilder("date_utc,page,page_key,culture,helpful,reason,comment\r\n");
        foreach (var r in store.All(Since(days)))
        {
            if (!names.TryGetValue(r.PageKey, out var name)) names[r.PageKey] = name = Page(cref.UmbracoContext, r.PageKey).Name;
            csv.Append(string.Join(',',
                r.CreatedUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), Cell(name), r.PageKey, r.Culture,
                r.Helpful ? "yes" : "no", r.Reason ?? "", Cell(r.Comment))).Append("\r\n");
        }
        // BOM so Excel opens Arabic text correctly
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8", $"page-feedback-{clock.GetUtcNow():yyyyMMdd}.csv");
    }

    /// <summary>CSV cell: quoted, and never starting with a formula character (spreadsheet injection).</summary>
    public static string Cell(string? value)
    {
        var v = value ?? "";
        if (v.Length > 0 && "=+-@\t\r".Contains(v[0])) v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}
