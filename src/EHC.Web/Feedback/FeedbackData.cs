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

namespace EHC.Web.Feedback;

/// <summary>
/// One "Was this page helpful?" answer. Deliberately no personal data: no name, e-mail, IP address, user agent or
/// visitor id — only the page, language, answer, reason, an optional redacted comment and the time.
/// </summary>
[TableName(Table)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class FeedbackDto
{
    public const string Table = "ehcFeedback";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("pageKey")]
    [Index(IndexTypes.NonClustered, Name = "IX_ehcFeedback_pageKey")]
    public Guid PageKey { get; set; }

    [Column("culture")]
    [Length(10)]
    public string Culture { get; set; } = "";

    [Column("helpful")]
    public bool Helpful { get; set; }

    [Column("reason")]
    [Length(20)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Reason { get; set; }

    [Column("comment")]
    [Length(300)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Comment { get; set; }

    [Column("createdUtc")]
    [Index(IndexTypes.NonClustered, Name = "IX_ehcFeedback_createdUtc")]
    public DateTime CreatedUtc { get; set; }
}

public sealed class AddFeedbackTable(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists(FeedbackDto.Table))
        {
            Create.Table<FeedbackDto>().Do();
        }
        return Task.CompletedTask;
    }
}

/// <summary>Creates / upgrades the feedback table on startup (runs once per migration step, tracked by Umbraco).</summary>
public sealed class FeedbackMigrationRunner(
    ICoreScopeProvider scopes,
    IMigrationPlanExecutor executor,
    IKeyValueService keyValues,
    IRuntimeState runtime) : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (runtime.Level < RuntimeLevel.Run) return;
        var plan = new MigrationPlan("EHC.Feedback");
        plan.From(string.Empty).To<AddFeedbackTable>("ehc-feedback-1");
        await new Upgrader(plan).ExecuteAsync(executor, scopes, keyValues);
    }
}
