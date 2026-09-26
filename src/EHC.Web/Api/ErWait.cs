using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace EHC.Web.Api;

/// <summary>One ER's current estimate, keyed by the facility's erFeedId.</summary>
public sealed record ErWait(string FeedId, int Minutes, string Level, DateTime UpdatedUtc);

public sealed record ErWaitResponse(bool Available, bool Illustrative, IReadOnlyList<ErWait> Items);

/// <summary>
/// Source of ER wait times. The real implementation calls the EHC feed (command centre / HIS — to be identified)
/// server-side via IHttpClientFactory with timeouts and resilience; secrets come from configuration, never the repo.
/// </summary>
public interface IErWaitProvider
{
    Task<ErWaitResponse> GetAsync(CancellationToken cancellationToken);
}

public sealed class ErWaitOptions
{
    /// <summary>Development only: return clearly-labelled illustrative numbers so the block can be designed and tested.</summary>
    public bool UseDemoData { get; set; }
}

/// <summary>
/// Stub until a real feed exists: returns "not available" (the block shows a coming-soon state), or — when
/// Ehc:ErWait:UseDemoData is true — illustrative numbers flagged as such. Never enable demo data in production.
/// </summary>
public sealed class StubErWaitProvider(IOptions<ErWaitOptions> options, TimeProvider clock) : IErWaitProvider
{
    public Task<ErWaitResponse> GetAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.UseDemoData)
            return Task.FromResult(new ErWaitResponse(false, false, []));

        var now = clock.GetUtcNow().UtcDateTime;
        // stable per quarter hour so the page doesn't jump on every refresh
        var seed = (int)(now.Ticks / TimeSpan.FromMinutes(15).Ticks);
        var items = Enumerable.Range(1, 6).Select(i =>
        {
            var minutes = 10 + (Math.Abs(HashCode.Combine(seed, i)) % 70);
            return new ErWait($"demo-{i}", minutes, ErLevel.For(minutes), now);
        }).ToList();
        return Task.FromResult(new ErWaitResponse(true, true, items));
    }
}

public static class ErLevel
{
    public static string For(int minutes) => minutes < 30 ? "quiet" : minutes < 55 ? "moderate" : "busy";
}

/// <summary>GET /api/er-wait — cached 60 s server-side.</summary>
[ApiController]
[Route("api/er-wait")]
[EnableRateLimiting(ApiSetup.Policy)]
public sealed class ErWaitController(IErWaitProvider provider, IMemoryCache cache) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ErWaitResponse>> Get(CancellationToken cancellationToken)
    {
        var result = await cache.GetOrCreateAsync("ehc:er-wait", e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return provider.GetAsync(cancellationToken);
        });
        Response.Headers.CacheControl = "public, max-age=30";
        return Ok(result);
    }
}
