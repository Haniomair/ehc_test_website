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

namespace EHC.Web.Vitals;

/// <summary>
/// One Core Web Vitals value from a real visit. No personal data: no IP address, user agent, cookie or visitor id —
/// only the page, language, device class (mobile / desktop), the metric, its value and the time.
/// </summary>
[TableName(Table)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class VitalDto
{
    public const string Table = "ehcVitals";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("pageKey")]
    [Index(IndexTypes.NonClustered, Name = "IX_ehcVitals_pageKey")]
    public Guid PageKey { get; set; }

    [Column("culture")]
    [Length(10)]
    public string Culture { get; set; } = "";

    [Column("device")]
    [Length(10)]
    public string Device { get; set; } = "";

    [Column("metric")]
    [Length(5)]
    public string Metric { get; set; } = "";

    /// <summary>Milliseconds for LCP and INP; unitless score for CLS.</summary>
    [Column("value")]
    public double Value { get; set; }

    [Column("createdUtc")]
    [Index(IndexTypes.NonClustered, Name = "IX_ehcVitals_createdUtc")]
    public DateTime CreatedUtc { get; set; }
}

public sealed class AddVitalsTable(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists(VitalDto.Table))
        {
            Create.Table<VitalDto>().Do();
        }
        return Task.CompletedTask;
    }
}

/// <summary>Creates the vitals table on startup (runs once per migration step, tracked by Umbraco).</summary>
public sealed class VitalsMigrationRunner(
    ICoreScopeProvider scopes,
    IMigrationPlanExecutor executor,
    IKeyValueService keyValues,
    IRuntimeState runtime) : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (runtime.Level < RuntimeLevel.Run) return;
        var plan = new MigrationPlan("EHC.Vitals");
        plan.From(string.Empty).To<AddVitalsTable>("ehc-vitals-1");
        await new Upgrader(plan).ExecuteAsync(executor, scopes, keyValues);
    }
}
