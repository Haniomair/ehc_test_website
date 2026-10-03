using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Extensions;

namespace EHC.Web.Vitals;

/// <summary>Deletes values older than the retention period (default 90 days), once a day, on one server.</summary>
public sealed class VitalsCleanupJob(IVitalsStore store, IOptions<VitalsOptions> options, TimeProvider clock, ILogger<VitalsCleanupJob> logger) : IRecurringBackgroundJob
{
    public TimeSpan Period => TimeSpan.FromDays(1);
    public TimeSpan Delay => TimeSpan.FromMinutes(7);
    public ServerRole[] ServerRoles => [ServerRole.Single, ServerRole.SchedulingPublisher];

    public event EventHandler PeriodChanged { add { } remove { } }

    public Task RunJobAsync()
    {
        var days = Math.Clamp(options.Value.RetentionDays, 28, 400);
        var removed = store.DeleteOlderThan(clock.GetUtcNow().UtcDateTime.AddDays(-days));
        if (removed > 0) logger.LogInformation("Core Web Vitals: removed {Count} values older than {Days} days", removed, days);
        return Task.CompletedTask;
    }
}

public static class VitalsSetup
{
    public static void Add(IUmbracoBuilder builder)
    {
        builder.Services.Configure<VitalsOptions>(builder.Config.GetSection("Ehc:Vitals"));
        builder.Services.AddSingleton<IVitalsStore, VitalsStore>();
        builder.Services.AddRecurringBackgroundJob<VitalsCleanupJob>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, VitalsMigrationRunner>();
    }
}
