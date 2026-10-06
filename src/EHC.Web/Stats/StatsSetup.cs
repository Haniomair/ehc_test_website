using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Extensions;

namespace EHC.Web.Stats;

/// <summary>
/// Once an hour: stores the totals of every finished day that is not stored yet, then deletes single page views older
/// than the retention period (never before their day is stored). Runs on one server.
/// </summary>
public sealed class StatsRollupJob(IStatsStore store, IStatsFunnels funnels, StatsCalendar calendar, IOptions<StatsOptions> options, TimeProvider clock, ILogger<StatsRollupJob> logger)
    : IRecurringBackgroundJob
{
    public TimeSpan Period => TimeSpan.FromHours(1);
    public TimeSpan Delay => TimeSpan.FromMinutes(3);
    public ServerRole[] ServerRoles => [ServerRole.Single, ServerRole.SchedulingPublisher];

    public event EventHandler PeriodChanged { add { } remove { } }

    public Task RunJobAsync()
    {
        // a day is final ten minutes after midnight, once the last queued page views are written
        var settled = calendar.DayOf(clock.GetUtcNow().UtcDateTime.AddMinutes(-10));
        var next = store.RolledUpTo()?.AddDays(1) ?? (store.FirstHitUtc() is { } first ? calendar.DayOf(first) : settled);
        for (var day = next; day < settled; day = day.AddDays(1))
        {
            // funnels first: SaveDay marks the day as done
            funnels.SaveDay(day);
            store.SaveDay(day, store.Aggregate(calendar.StartUtc(day), calendar.StartUtc(day.AddDays(1))));
            next = day.AddDays(1);
        }

        var keepFrom = calendar.Today.AddDays(-Math.Clamp(options.Value.RawRetentionDays, 7, 400));
        if (next < keepFrom) keepFrom = next;
        var removed = store.DeleteHitsBefore(calendar.StartUtc(keepFrom));
        if (removed > 0) logger.LogInformation("Visitor statistics: removed {Count} page views from before {Day}", removed, keepFrom);
        return Task.CompletedTask;
    }
}

/// <summary>
/// First run only: gives administrators the Statistics section and creates an "Analytics" user group that has only
/// that section. Later changes made in the backoffice (Users → User groups) are never overridden.
/// </summary>
public sealed class StatsSectionSeeder(
    IUserGroupService groups,
    IShortStringHelper strings,
    IKeyValueService keyValues,
    IRuntimeState runtime,
    ILogger<StatsSectionSeeder> logger) : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private const string DoneKey = "Ehc.Stats.SectionSeeded";

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtime.Level != RuntimeLevel.Run || keyValues.GetValue(DoneKey) is not null) return;
        var user = Constants.Security.SuperUserKey;

        if (await groups.GetAsync(Constants.Security.AdminGroupAlias) is { } admin && !admin.AllowedSections.Contains(StatsSetup.Section))
        {
            admin.AddAllowedSection(StatsSetup.Section);
            var updated = await groups.UpdateAsync(admin, user);
            if (!updated.Success) logger.LogWarning("Could not add the Statistics section to administrators: {Status}", updated.Status);
        }

        if (await groups.GetAsync(StatsSetup.AnalyticsGroup) is null)
        {
            var group = new UserGroup(strings) { Alias = StatsSetup.AnalyticsGroup, Name = "Analytics", Icon = "icon-chart" };
            group.AddAllowedSection(StatsSetup.Section);
            var created = await groups.CreateAsync(group, user, []);
            if (!created.Success) logger.LogWarning("Could not create the Analytics user group: {Status}", created.Status);
        }

        keyValues.SetValue(DoneKey, "1");
        logger.LogInformation("Visitor statistics: Statistics section given to administrators and the Analytics group");
    }
}

/// <summary>The signed-in backoffice user belongs to a group that has the Statistics section.</summary>
public sealed class StatsSectionRequirement : IAuthorizationRequirement;

public sealed class StatsSectionHandler(IAuthorizationHelper helper) : AuthorizationHandler<StatsSectionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, StatsSectionRequirement requirement)
    {
        if (helper.TryGetUmbracoUser(context.User, out var user) && user.AllowedSections.Contains(StatsSetup.Section))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public static class StatsSetup
{
    /// <summary>Backoffice section alias (wwwroot/App_Plugins/EhcStats/umbraco-package.json); access is set per user group.</summary>
    public const string Section = "Ehc.Section.Statistics";
    public const string AnalyticsGroup = "ehcAnalytics";
    public const string Policy = "EhcStatisticsSection";

    public static void Add(IUmbracoBuilder builder)
    {
        var services = builder.Services;
        services.Configure<StatsOptions>(builder.Config.GetSection("Ehc:Stats"));
        services.AddSingleton(sp => new StatsCalendar(
            StatsCalendar.Zone(sp.GetRequiredService<IOptions<StatsOptions>>().Value.TimeZone), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IStatsStore, StatsStore>();
        services.AddSingleton<StatsReports>();
        services.AddSingleton<IStatsFunnels, StatsFunnels>();
        services.AddSingleton<StatsVisitors>();
        services.AddSingleton<IStatsGeo, StatsGeo>();
        services.AddSingleton<StatsQueue>();
        services.AddSingleton<StatsLive>();
        services.AddHostedService<StatsWriter>();
        services.AddHttpClient(StatsGeoUpdateJob.HttpClientName, c => c.Timeout = TimeSpan.FromMinutes(10));
        services.AddRecurringBackgroundJob<StatsRollupJob>();
        services.AddRecurringBackgroundJob<StatsGeoUpdateJob>();
        // like Umbraco's own section policies: a backoffice token whose user's groups include the Statistics section
        services.AddSingleton<IAuthorizationHandler, StatsSectionHandler>();
        services.AddAuthorization(o => o.AddPolicy(Policy, policy =>
        {
            policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new StatsSectionRequirement());
        }));
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, StatsMigrationRunner>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, StatsSectionSeeder>();
    }
}
