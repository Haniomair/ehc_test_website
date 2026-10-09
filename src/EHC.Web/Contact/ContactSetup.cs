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

namespace EHC.Web.Contact;

/// <summary>Deletes messages older than the retention period (default 12 months), once a day, on one server.</summary>
public sealed class ContactCleanupJob(IContactStore store, IOptions<ContactOptions> options, TimeProvider clock, ILogger<ContactCleanupJob> logger) : IRecurringBackgroundJob
{
    public TimeSpan Period => TimeSpan.FromDays(1);
    public TimeSpan Delay => TimeSpan.FromMinutes(6);
    public ServerRole[] ServerRoles => [ServerRole.Single, ServerRole.SchedulingPublisher];

    public event EventHandler PeriodChanged { add { } remove { } }

    public Task RunJobAsync()
    {
        var months = Math.Clamp(options.Value.RetentionMonths, 1, 120);
        var removed = store.DeleteOlderThan(clock.GetUtcNow().UtcDateTime.AddMonths(-months));
        if (removed > 0) logger.LogInformation("Contact form: removed {Count} messages older than {Months} months", removed, months);
        return Task.CompletedTask;
    }
}

/// <summary>
/// First run only: gives administrators the Messages section and creates a "Patient experience" user group that has only
/// that section. Later changes made in the backoffice (Users → User groups) are never overridden.
/// </summary>
public sealed class ContactSectionSeeder(
    IUserGroupService groups,
    IShortStringHelper strings,
    IKeyValueService keyValues,
    IRuntimeState runtime,
    ILogger<ContactSectionSeeder> logger) : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private const string DoneKey = "Ehc.Contact.SectionSeeded";

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtime.Level != RuntimeLevel.Run || keyValues.GetValue(DoneKey) is not null) return;
        var user = Constants.Security.SuperUserKey;

        if (await groups.GetAsync(Constants.Security.AdminGroupAlias) is { } admin && !admin.AllowedSections.Contains(ContactSetup.Section))
        {
            admin.AddAllowedSection(ContactSetup.Section);
            var updated = await groups.UpdateAsync(admin, user);
            if (!updated.Success) logger.LogWarning("Could not add the Messages section to administrators: {Status}", updated.Status);
        }

        if (await groups.GetAsync(ContactSetup.PatientExperienceGroup) is null)
        {
            var group = new UserGroup(strings) { Alias = ContactSetup.PatientExperienceGroup, Name = "Patient experience", Icon = "icon-message" };
            group.AddAllowedSection(ContactSetup.Section);
            var created = await groups.CreateAsync(group, user, []);
            if (!created.Success) logger.LogWarning("Could not create the Patient experience user group: {Status}", created.Status);
        }

        keyValues.SetValue(DoneKey, "1");
        logger.LogInformation("Contact form: Messages section given to administrators and the Patient experience group");
    }
}

/// <summary>The signed-in backoffice user belongs to a group that has the Messages section.</summary>
public sealed class ContactSectionRequirement : IAuthorizationRequirement;

public sealed class ContactSectionHandler(IAuthorizationHelper helper) : AuthorizationHandler<ContactSectionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ContactSectionRequirement requirement)
    {
        if (helper.TryGetUmbracoUser(context.User, out var user) && user.AllowedSections.Contains(ContactSetup.Section))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public static class ContactSetup
{
    /// <summary>Backoffice section alias (wwwroot/App_Plugins/EhcContact/umbraco-package.json); access is set per user group.</summary>
    public const string Section = "Ehc.Section.Messages";
    public const string PatientExperienceGroup = "ehcPatientExperience";
    public const string Policy = "EhcMessagesSection";

    public static void Add(IUmbracoBuilder builder)
    {
        var services = builder.Services;
        services.Configure<ContactOptions>(builder.Config.GetSection("Ehc:Contact"));
        services.AddSingleton<IContactStore, ContactStore>();
        services.AddRecurringBackgroundJob<ContactCleanupJob>();
        // like Umbraco's own section policies: a backoffice token whose user's groups include the Messages section
        services.AddSingleton<IAuthorizationHandler, ContactSectionHandler>();
        services.AddAuthorization(o => o.AddPolicy(Policy, policy =>
        {
            policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new ContactSectionRequirement());
        }));
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, ContactMigrationRunner>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, ContactSectionSeeder>();
        builder.AddNotificationHandler<UmbracoApplicationStartedNotification, ContactPageSeeder>();
    }
}
