using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;

namespace EHC.Web.Contact;

public sealed record ContactInput(Guid PageKey, string? Culture, string? Service, string? Name, string? Email, string? Phone, string? Message, bool Consent, string? Website);

public sealed record ContactResult(string Reference);

/// <summary>
/// POST /api/contact — the "Contact us" form. JSON only (so other sites can't post it with a plain form), rate limited
/// per IP (the address is used only in memory for the limit, never stored), page must be published, service from a
/// fixed list, every field cleaned and checked (ContactText). Invalid fields come back by name so the form can mark
/// them. "Website" is a honeypot field that people never see.
/// </summary>
[ApiController]
[Route("api/contact")]
[EnableRateLimiting(Api.ApiSetup.ContactPolicy)]
public sealed class ContactController(IContactStore store, IPublishedContentQuery content, TimeProvider clock, ILogger<ContactController> logger) : ControllerBase
{
    private static readonly string[] Cultures = ["ar-SA", "en-US"];

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Post([FromBody] ContactInput input)
    {
        Response.Headers.CacheControl = "no-store";
        // bot: looks accepted, nothing is stored
        if (!string.IsNullOrEmpty(input.Website)) return Ok(new ContactResult(ContactText.NewReference(clock.GetUtcNow().UtcDateTime)));
        if (input.PageKey == Guid.Empty || content.Content(input.PageKey) is null) return BadRequest();
        var culture = Cultures.FirstOrDefault(c => string.Equals(c, input.Culture, StringComparison.OrdinalIgnoreCase));
        if (culture is null) return BadRequest();

        var message = new ContactMessageDto
        {
            Service = ContactText.Service(input.Service) ?? "",
            Name = ContactText.Name(input.Name) ?? "",
            Email = ContactText.Email(input.Email) ?? "",
            Phone = ContactText.Phone(input.Phone) ?? "",
            Message = ContactText.Message(input.Message) ?? "",
            Culture = culture,
            PageKey = input.PageKey,
            CreatedUtc = clock.GetUtcNow().UtcDateTime,
        };
        var errors = new Dictionary<string, string>();
        if (message.Service.Length == 0) errors["service"] = "required";
        if (message.Name.Length == 0) errors["name"] = string.IsNullOrWhiteSpace(input.Name) ? "required" : "invalid";
        if (message.Email.Length == 0) errors["email"] = string.IsNullOrWhiteSpace(input.Email) ? "required" : "invalid";
        if (message.Phone.Length == 0) errors["phone"] = string.IsNullOrWhiteSpace(input.Phone) ? "required" : "invalid";
        if (message.Message.Length == 0) errors["message"] = string.IsNullOrWhiteSpace(input.Message) ? "required" : "invalid";
        if (!input.Consent) errors["consent"] = "required";
        if (errors.Count > 0) return BadRequest(new { errors });

        var reference = await store.AddAsync(message);
        logger.LogInformation("Contact form: message {Reference} received ({Service})", reference, message.Service);
        return Ok(new ContactResult(reference));
    }
}
