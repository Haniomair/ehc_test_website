using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Umbraco.Cms.Core;

namespace EHC.Web.Feedback;

public sealed record FeedbackInput(Guid PageKey, string? Culture, bool Helpful, string? Reason, string? Comment, string? Website);

/// <summary>
/// POST /api/feedback — "Was this page helpful?". JSON only (so other sites can't post it with a plain form), rate
/// limited per IP (the address is used only in memory for the limit, never stored), page must be published, reason
/// from a fixed list, comment cleaned (FeedbackText). "Website" is a honeypot field that people never see.
/// </summary>
[ApiController]
[Route("api/feedback")]
[EnableRateLimiting(Api.ApiSetup.FeedbackPolicy)]
public sealed class FeedbackController(IFeedbackStore store, IPublishedContentQuery content, TimeProvider clock) : ControllerBase
{
    private static readonly string[] Cultures = ["ar-SA", "en-US"];

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Post([FromBody] FeedbackInput input)
    {
        Response.Headers.CacheControl = "no-store";
        if (!string.IsNullOrEmpty(input.Website)) return NoContent();   // bot: accept silently, store nothing
        if (input.PageKey == Guid.Empty || content.Content(input.PageKey) is null) return BadRequest();
        var culture = Cultures.FirstOrDefault(c => string.Equals(c, input.Culture, StringComparison.OrdinalIgnoreCase));
        if (culture is null) return BadRequest();

        await store.AddAsync(new FeedbackDto
        {
            PageKey = input.PageKey,
            Culture = culture,
            Helpful = input.Helpful,
            Reason = input.Helpful ? null : FeedbackText.Reason(input.Reason),
            Comment = input.Helpful ? null : FeedbackText.Comment(input.Comment),
            CreatedUtc = clock.GetUtcNow().UtcDateTime,
        });
        return NoContent();
    }
}
