using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Extensions;

namespace EHC.Web.Feedback;

/// <summary>Deletes answers older than the retention period (default 12 months), once a day, on one server.</summary>
public sealed class FeedbackCleanupJob(IFeedbackStore store, IOptions<FeedbackOptions> options, TimeProvider clock, ILogger<FeedbackCleanupJob> logger) : IRecurringBackgroundJob
{
    public TimeSpan Period => TimeSpan.FromDays(1);
    public TimeSpan Delay => TimeSpan.FromMinutes(5);
    public ServerRole[] ServerRoles => [ServerRole.Single, ServerRole.SchedulingPublisher];

    public event EventHandler PeriodChanged { add { } remove { } }

    public Task RunJobAsync()
    {
        var months = Math.Clamp(options.Value.RetentionMonths, 1, 120);
        var removed = store.DeleteOlderThan(clock.GetUtcNow().UtcDateTime.AddMonths(-months));
        if (removed > 0) logger.LogInformation("Page feedback: removed {Count} answers older than {Months} months", removed, months);
        return Task.CompletedTask;
    }
}

public static class FeedbackSetup
{
    public static void Add(IUmbracoBuilder builder)
    {
        builder.Services.Configure<FeedbackOptions>(builder.Config.GetSection("Ehc:Feedback"));
        builder.Services.AddSingleton<IFeedbackStore, FeedbackStore>();
        builder.Services.AddRecurringBackgroundJob<FeedbackCleanupJob>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, FeedbackMigrationRunner>();
    }
}
