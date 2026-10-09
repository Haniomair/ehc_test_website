using System.Globalization;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using EHC.Web.Feedback;

namespace EHC.Web.Contact;

public sealed record ContactMessage(int Id, string Reference, string Service, string Name, string Email, string Phone, string Message,
    string Culture, string Page, string? PageUrl, string Status, DateTime CreatedUtc, DateTime? UpdatedUtc);

public sealed record ContactStatusInput(string? Status);

/// <summary>
/// Backoffice API for the Messages section (user groups with that section only, as the messages hold personal data):
/// /umbraco/management/api/v1/ehc/contact/{messages|counts|messages/{id}/status|export}.
/// </summary>
[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("ehc/contact")]
[ApiExplorerSettings(GroupName = "EHC")]
[Authorize(Policy = ContactSetup.Policy)]
public sealed class ContactManagementController(IContactStore store, IUmbracoContextFactory contexts, TimeProvider clock) : ManagementApiControllerBase
{
    private DateTime Since(int days) => clock.GetUtcNow().UtcDateTime.AddDays(-Math.Clamp(days, 1, 800));

    [HttpGet("messages")]
    public ActionResult<IEnumerable<ContactMessage>> Messages(int days = 90, string? service = null, string? status = null, int take = 200)
    {
        var filter = new ContactFilter { SinceUtc = Since(days), Service = ContactText.Service(service), Status = ContactText.Status(status) };
        using var cref = contexts.EnsureUmbracoContext();
        return Ok(store.List(filter, take).Select(m => Map(cref.UmbracoContext, m)).ToList());
    }

    [HttpGet("counts")]
    public ActionResult<IReadOnlyDictionary<string, int>> Counts(int days = 90)
    {
        var counts = store.Counts(Since(days));
        return Ok(ContactText.Statuses.ToDictionary(s => s, s => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0));
    }

    [HttpPut("messages/{id:int}/status")]
    public IActionResult SetStatus(int id, [FromBody] ContactStatusInput input)
    {
        var status = ContactText.Status(input.Status);
        if (status is null) return BadRequest();
        return store.SetStatus(id, status, clock.GetUtcNow().UtcDateTime) ? NoContent() : NotFound();
    }

    [HttpGet("export")]
    public IActionResult Export(int days = 365, string? service = null, string? status = null)
    {
        var filter = new ContactFilter { SinceUtc = Since(days), Service = ContactText.Service(service), Status = ContactText.Status(status) };
        using var cref = contexts.EnsureUmbracoContext();
        var csv = new StringBuilder("date_utc,reference,service,helpdesk_code,status,name,email,phone,message,culture,page\r\n");
        foreach (var m in store.List(filter, 5000).Select(m => Map(cref.UmbracoContext, m)))
        {
            var code = ContactText.Services.FirstOrDefault(s => s.Key == m.Service)?.Code ?? "";
            csv.Append(string.Join(',',
                m.CreatedUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), m.Reference, m.Service, code, m.Status,
                FeedbackManagementController.Cell(m.Name), FeedbackManagementController.Cell(m.Email), FeedbackManagementController.Cell(m.Phone),
                FeedbackManagementController.Cell(m.Message), m.Culture, FeedbackManagementController.Cell(m.Page))).Append("\r\n");
        }
        // BOM so Excel opens Arabic text correctly
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8", $"contact-messages-{clock.GetUtcNow():yyyyMMdd}.csv");
    }

    private static ContactMessage Map(IUmbracoContext ctx, ContactMessageDto m)
    {
        var page = ctx.Content?.GetById(m.PageKey);
        return new ContactMessage(m.Id, m.Reference, m.Service, m.Name, m.Email, m.Phone, m.Message, m.Culture,
            page is null ? "(deleted page)" : page.Name ?? m.PageKey.ToString(), page?.Url(), m.Status, m.CreatedUtc, m.UpdatedUtc);
    }
}
