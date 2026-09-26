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

    public static void Add(IUmbracoBuilder builder)
    {
        var services = builder.Services;
        services.AddMemoryCache();
        services.AddOutputCache();
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(Policy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
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
