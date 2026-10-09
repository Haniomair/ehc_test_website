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

namespace EHC.Web.Contact;

/// <summary>
/// One message sent with the "Contact us" form (complaint, question, suggestion…). Holds the personal data the visitor
/// typed (name, e-mail, phone, message) and nothing else about them: no IP address, user agent or visitor id.
/// Readable only in the Messages section, deleted after the retention period.
/// </summary>
[TableName(Table)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class ContactMessageDto
{
    public const string Table = "ehcContactMessage";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("reference")]
    [Length(20)]
    [Index(IndexTypes.UniqueNonClustered, Name = "IX_ehcContactMessage_reference")]
    public string Reference { get; set; } = "";

    [Column("service")]
    [Length(20)]
    public string Service { get; set; } = "";

    [Column("name")]
    [Length(ContactText.MaxName)]
    public string Name { get; set; } = "";

    [Column("email")]
    [Length(ContactText.MaxEmail)]
    public string Email { get; set; } = "";

    [Column("phone")]
    [Length(20)]
    public string Phone { get; set; } = "";

    [Column("message")]
    [Length(ContactText.MaxMessage)]
    public string Message { get; set; } = "";

    [Column("culture")]
    [Length(10)]
    public string Culture { get; set; } = "";

    [Column("pageKey")]
    public Guid PageKey { get; set; }

    [Column("status")]
    [Length(20)]
    public string Status { get; set; } = ContactText.StatusNew;

    [Column("createdUtc")]
    [Index(IndexTypes.NonClustered, Name = "IX_ehcContactMessage_createdUtc")]
    public DateTime CreatedUtc { get; set; }

    [Column("updatedUtc")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? UpdatedUtc { get; set; }
}

public sealed class AddContactMessageTable(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists(ContactMessageDto.Table))
        {
            Create.Table<ContactMessageDto>().Do();
        }
        return Task.CompletedTask;
    }
}

/// <summary>Creates / upgrades the contact message table on startup (runs once per migration step, tracked by Umbraco).</summary>
public sealed class ContactMigrationRunner(
    ICoreScopeProvider scopes,
    IMigrationPlanExecutor executor,
    IKeyValueService keyValues,
    IRuntimeState runtime) : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (runtime.Level < RuntimeLevel.Run) return;
        var plan = new MigrationPlan("EHC.Contact");
        plan.From(string.Empty).To<AddContactMessageTable>("ehc-contact-1");
        await new Upgrader(plan).ExecuteAsync(executor, scopes, keyValues);
    }
}
