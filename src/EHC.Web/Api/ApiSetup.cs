using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace EHC.Web.Api;

/// <summary>Services + middleware for the public, read-only APIs: rate limit per IP, output cache, ER provider.</summary>
public static class ApiSetup
{
    public const string Policy = "ehc-api";
    public const string FeedbackPolicy = "ehc-feedback";
    public const string VitalsPolicy = "ehc-vitals";
    public const string StatsPolicy = "ehc-stats";
    public const int ReadPermitsPerMinute = 600;

    public static void Add(IUmbracoBuilder builder)
    {
        var services = builder.Services;
        services.AddMemoryCache();
        services.AddOutputCache();
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // read-only, output-cached endpoints: counted per IP, and hospitals / offices share one public IP,
            // so the limit only stops scripted floods, not a busy network of real visitors
            o.AddPolicy(Policy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = ReadPermitsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            // page feedback: a person answers a handful of pages; anything more is automated
            o.AddPolicy(FeedbackPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
            // Core Web Vitals: one small report per page view; hospitals and offices share one public IP
            o.AddPolicy(VitalsPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            // visitor statistics: one small report per page view, same allowance as the vitals
            o.AddPolicy(StatsPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        services.Configure<ErWaitOptions>(builder.Config.GetSection("Ehc:ErWait"));
        services.AddSingleton<IErWaitProvider, StubErWaitProvider>();

        services.Configure<UmbracoPipelineOptions>(o => o.AddFilter(new UmbracoPipelineFilter("EhcApi")
        {
            PostRouting = app =>
            {
                app.UseRateLimiter();
                app.UseOutputCache();
            },
        }));
    }
}
