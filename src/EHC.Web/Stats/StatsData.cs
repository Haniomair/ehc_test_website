using NPoco;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace EHC.Web.Stats;

/// <summary>
/// One page view. No IP address, user-agent string, cookie or full referring URL is stored: the visitor id is a hash
/// that changes every day (StatsVisitors), the place is a country (plus region and city inside Saudi Arabia only), and
/// the rest are coarse groups. Kept for a limited time (Ehc:Stats:RawRetentionDays), then only the daily totals remain.
/// </summary>
[TableName(Table)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class StatsHitDto
{
    public const string Table = "ehcStatsHit";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("createdUtc")]
    [Index(IndexTypes.NonClustered, Name = "IX_ehcStatsHit_createdUtc")]
    public DateTime CreatedUtc { get; set; }

    [Column("pageKey")]
    public Guid PageKey { get; set; }

    [Column("culture")]
    [Length(10)]
    public string Culture { get; set; } = "";

    /// <summary>Daily visitor hash: the same browser on the same day only, never linkable across days.</summary>
    [Column("visitor")]
    [Length(16)]
    public string Visitor { get; set; } = "";

    /// <summary>First page of a visit (no page view by this visitor in the last 30 minutes).</summary>
    [Column("newVisit")]
    public bool NewVisit { get; set; }

    /// <summary>Set on the first page of a visit only.</summary>
    [Column("medium")]
    [Length(10)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Medium { get; set; }

    [Column("source")]
    [Length(100)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Source { get; set; }

    [Column("campaign")]
    [Length(100)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Campaign { get; set; }

    /// <summary>ISO 3166 code; null when unknown.</summary>
    [Column("country")]
    [Length(2)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Country { get; set; }

    [Column("region")]
    [Length(60)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Region { get; set; }

    [Column("city")]
    [Length(60)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? City { get; set; }

    [Column("device")]
    [Length(10)]
    public string Device { get; set; } = "";

    [Column("browser")]
    [Length(20)]
    public string Browser { get; set; } = "";

    [Column("os")]
    [Length(20)]
    public string Os { get; set; } = "";
}

/// <summary>
/// Daily totals per dimension (see StatsStore.Dimensions), written by StatsRollupJob once a day is over and kept for good.
/// Visitors are unique within the day.
/// </summary>
[TableName(Table)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class StatsDailyDto
{
    public const string Table = "ehcStatsDaily";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>Calendar day in the site's time zone (Ehc:Stats:TimeZone), at midnight.</summary>
    [Column("day")]
    [Index(IndexTypes.NonClustered, Name = "IX_ehcStatsDaily_day")]
    public DateTime Day { get; set; }

    [Column("dimension")]
    [Length(16)]
    public string Dimension { get; set; } = "";

    [Column("value")]
    [Length(200)]
    public string Value { get; set; } = "";

    [Column("views")]
    public int Views { get; set; }

    [Column("visits")]
    public int Visits { get; set; }

    [Column("visitors")]
    public int Visitors { get; set; }
}

public sealed class AddStatsTables(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists(StatsHitDto.Table))
        {
            Create.Table<StatsHitDto>().Do();
        }
        if (!TableExists(StatsDailyDto.Table))
        {
            Create.Table<StatsDailyDto>().Do();
        }
        return Task.CompletedTask;
    }
}

/// <summary>Creates the statistics tables on startup (runs once per migration step, tracked by Umbraco).</summary>
public sealed class StatsMigrationRunner(
    ICoreScopeProvider scopes,
    IMigrationPlanExecutor executor,
    IKeyValueService keyValues,
    IRuntimeState runtime) : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (runtime.Level < RuntimeLevel.Run) return;
        var plan = new MigrationPlan("EHC.Stats");
        plan.From(string.Empty)
            .To<AddStatsTables>("ehc-stats-1")
            .To<AddStatsFunnelTables>("ehc-stats-2");
        await new Upgrader(plan).ExecuteAsync(executor, scopes, keyValues);
    }
}
